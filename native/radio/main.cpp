// SPDX-License-Identifier: GPL-2.0-only
// AirDrop's independent USB process, linked with OpenIPC/devourer.
#include <windows.h>
#include <libusb.h>
#include <atomic>
#include <algorithm>
#include <condition_variable>
#include <deque>
#include <iostream>
#include <mutex>
#include <thread>
#include <vector>
#include <fstream>
#include <chrono>
#include "WiFiDriver.h"
#include "UsbOpen.h"
#include "UsbDeviceLock.h"
#include "RxPacket.h"
#include "radio_link.h"
#include "../common/pipe_io.h"

static std::atomic<bool> stopped{false};
static HANDLE stop_event = nullptr;
static BOOL WINAPI console_stop(DWORD) { stopped = true; if(stop_event) SetEvent(stop_event); return TRUE; }
static bool exact(HANDLE pipe, void* data, DWORD length, bool writing) {
    return air_pipe_exact(pipe,data,length,writing,stop_event) != 0;
}
int main(int argc,char** argv) {
    std::string mode = argc > 1 ? argv[1] : "--probe";
    if(mode != "--probe" && mode != "--listen" && mode != "--run") return 64;
    libusb_context* context = nullptr;
    if(libusb_init(&context)) return 1;
    auto handle = libusb_open_device_with_vid_pid(context,0x0bda,0xc811);
    if(!handle) { std::cerr << "USB c811 cannot be opened. Check the dedicated WinUSB binding.\n"; libusb_exit(context); return 2; }
    libusb_device_descriptor descriptor{};
    libusb_get_device_descriptor(libusb_get_device(handle),&descriptor);
    std::cout << "{\"event\":\"usb_open\",\"vid\":3034,\"pid\":51217,\"mode\":\"" << mode << "\"}" << std::endl;
    if(mode == "--probe") { libusb_close(handle); libusb_exit(context); return 0; }
    stop_event = CreateEventW(nullptr,TRUE,FALSE,L"Local\\AirDrop.Radio.Stop.v1");
    ResetEvent(stop_event); SetConsoleCtrlHandler(console_stop,TRUE);
    auto logger = std::make_shared<Logger>(); logger->set_level(Logger::Level::Warn);
    std::shared_ptr<devourer::UsbDeviceLock> lock;
    int iface = devourer::find_wifi_interface(handle);
    if(devourer::claim_interface_reset_reopen(context,handle,logger,true,lock) != 0) return 3;
    iface = devourer::find_wifi_interface(handle);
    devourer::DeviceConfig config{};
    config.rx.enable_with_tx = true; config.rx.keep_corrupted = false;
    config.tx.retry_limit = 7;
    auto radio = WiFiDriver(logger).CreateRadio(handle,context,lock,config);
    if(!radio) return 4;
    std::atomic<unsigned long long> received{0};
    std::ofstream capture;
    if(mode=="--listen" && argc>3) {
        capture.open(argv[3],std::ios::binary);
        const uint32_t pcap[]={0xa1b2c3d4,0x00040002,0,0,65535,127};
        capture.write(reinterpret_cast<const char*>(pcap),sizeof(pcap));
        if(!capture) { std::cerr<<"Cannot create passive capture\n"; return 6; }
    }
    std::mutex queue_mutex; std::condition_variable changed;
    std::deque<std::vector<uint8_t>> queue;
    auto callback = [&](const Packet& packet) {
        if(packet.RxAtrib.crc_err || packet.Data.size() < 24) return;
        ++received;
        size_t length = packet.Data.size();
        if(packet.RxAtrib.fcs_present && length >= 4) length -= 4;
        if(length + 8 > RADIO_LINK_MAX_PAYLOAD) return;
        std::vector<uint8_t> frame(8+length,0); frame[2] = 8; // Minimal valid radiotap.
        memcpy(frame.data()+8,packet.Data.data(),length);
        if(capture) {
            auto stamp=std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
            const uint32_t record[]={static_cast<uint32_t>(stamp/1000000),static_cast<uint32_t>(stamp%1000000),static_cast<uint32_t>(frame.size()),static_cast<uint32_t>(frame.size())};
            capture.write(reinterpret_cast<const char*>(record),sizeof(record));
            capture.write(reinterpret_cast<const char*>(frame.data()),frame.size());
        }
        if(mode != "--run") return;
        // AP beacon/probe traffic is irrelevant to AWDL and otherwise floods
        // the protocol daemon's warning log. Keep management action and data.
        auto control=packet.Data[0];
        if((control&0x0c)!=0x08 && (control&0xfc)!=0xd0) return;
        { std::lock_guard guard(queue_mutex); if(queue.size() == 1024) queue.pop_front(); queue.push_back(std::move(frame)); }
        changed.notify_one();
    };
    std::thread watcher([&] { WaitForSingleObject(stop_event,INFINITE); stopped = true; radio->StopRxLoop(); changed.notify_all(); });
    int result = 0;
    std::thread rx,sender;
    HANDLE pipe = INVALID_HANDLE_VALUE;
    try {
        SelectedChannel channel{149,0,CHANNEL_WIDTH_20};
        radio->InitWrite(channel);
        rx = std::thread([&] { try { radio->StartRxLoop(callback); } catch(...) { stopped = true; SetEvent(stop_event); } });
        std::cout << "{\"event\":\"radio_ready\",\"channel\":149,\"width\":20}" << std::endl;
        if(mode == "--listen") {
            int seconds = argc > 2 ? std::clamp(atoi(argv[2]),1,30) : 5;
            WaitForSingleObject(stop_event,seconds*1000);
        } else {
            pipe = CreateNamedPipeA(RADIO_LINK_PIPE_NAME,PIPE_ACCESS_DUPLEX|FILE_FLAG_OVERLAPPED,PIPE_TYPE_BYTE|PIPE_READMODE_BYTE|PIPE_WAIT|PIPE_REJECT_REMOTE_CLIENTS,1,262144,262144,0,nullptr);
            if(pipe == INVALID_HANDLE_VALUE) throw std::runtime_error("radio pipe creation failed");
            if(!air_pipe_connect(pipe,stop_event)) throw std::runtime_error("radio pipe connect failed");
            radio_link_header hello_header{RADIO_LINK_HELLO,0,sizeof(radio_link_hello)};
            radio_link_hello hello{RADIO_LINK_VERSION,149,20,{0,0}};
            exact(pipe,&hello_header,sizeof(hello_header),true); exact(pipe,&hello,sizeof(hello),true);
            sender = std::thread([&] {
                while(!stopped) {
                    std::vector<uint8_t> frame;
                    { std::unique_lock guard(queue_mutex); changed.wait_for(guard,std::chrono::milliseconds(100),[&]{return stopped || !queue.empty();}); if(queue.empty()) continue; frame = std::move(queue.front()); queue.pop_front(); }
                    radio_link_header header{RADIO_LINK_RX,0,static_cast<uint32_t>(frame.size())};
                    if(!exact(pipe,&header,sizeof(header),true) || !exact(pipe,frame.data(),static_cast<DWORD>(frame.size()),true)) { SetEvent(stop_event); break; }
                }
            });
            radio_link_header header{}; std::vector<uint8_t> payload(RADIO_LINK_MAX_PAYLOAD);
            devourer::MacAddr own{},bssid{{0x00,0x25,0x00,0xff,0x94,0x73}};
            while(!stopped && exact(pipe,&header,sizeof(header),false)) {
                if(header.reserved || header.length > payload.size() || !exact(pipe,payload.data(),header.length,false)) break;
                if(header.type == RADIO_LINK_TX && header.length >= 8) radio->send_packet(payload.data(),header.length);
                else if(header.type == RADIO_LINK_SET_CHANNEL && header.length == 1) { channel.Channel = payload[0]; radio->SetMonitorChannel(channel); }
                else if(header.type == RADIO_LINK_SET_ADDRESS && header.length == 6) {
                    memcpy(own.bytes.data(),payload.data(),6);
                    if(!radio->SetStationIdentity(own,bssid)) std::cerr << "Station identity could not be armed; unicast ACK behavior unverified.\n";
                }
                // Filter in the protocol daemon rather than excluding discovery traffic here.
            }
            SetEvent(stop_event); CancelSynchronousIo(sender.native_handle()); changed.notify_all();
            sender.join(); DisconnectNamedPipe(pipe); CloseHandle(pipe); pipe = INVALID_HANDLE_VALUE;
        }
        SetEvent(stop_event);
    } catch(const std::exception& error) { std::cerr << error.what() << '\n'; result = 5; SetEvent(stop_event); }
    radio->StopRxLoop(); changed.notify_all();
    if(sender.joinable()) { CancelSynchronousIo(sender.native_handle()); sender.join(); }
    if(rx.joinable()) rx.join();
    if(pipe != INVALID_HANDLE_VALUE) { DisconnectNamedPipe(pipe); CloseHandle(pipe); }
    radio->Stop();
    watcher.join();
    std::cout << "{\"event\":\"radio_stopped\",\"rx_frames\":" << received << "}" << std::endl;
    radio.reset(); libusb_release_interface(handle,iface); libusb_close(handle); libusb_exit(context); CloseHandle(stop_event);
    return result;
}
