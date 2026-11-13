// Original user-space IPv6/lwIP host and loopback TLS stream proxy.
// lwIP is independently licensed under its BSD notice (upstream/lwip/COPYING).
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <atomic>
#include <deque>
#include <iostream>
#include <mutex>
#include <thread>
#include <vector>
#include <algorithm>
#include "../common/pipe_io.h"
extern "C" {
#include "lwip/init.h"
#include "lwip/netif.h"
#include "lwip/ip6.h"
#include "lwip/tcp.h"
#include "lwip/udp.h"
#include "lwip/timeouts.h"
#include "lwip/mld6.h"
u32_t sys_now(void) { return static_cast<u32_t>(GetTickCount64()); }
}
static HANDLE pipe = INVALID_HANDLE_VALUE, stop_event = nullptr;
static std::atomic<bool> stopped{false};
static std::mutex in_lock,out_lock;
static std::deque<std::vector<uint8_t>> incoming,outgoing;
static bool exact(void* bytes,DWORD length,bool write) {
    return air_pipe_exact(pipe,bytes,length,write,stop_event) != 0;
}
static err_t output_packet(netif*,pbuf* packet,const ip6_addr_t*) {
    std::vector<uint8_t> copy(packet->tot_len); pbuf_copy_partial(packet,copy.data(),packet->tot_len,0);
    std::lock_guard guard(out_lock); if(outgoing.size()>=512) return ERR_MEM;
    outgoing.push_back(std::move(copy)); return ERR_OK;
}
static err_t init_interface(netif* device) {
    device->name[0]='a'; device->name[1]='d'; device->mtu=1450;
    device->output_ip6=output_packet; device->flags=NETIF_FLAG_MLD6; return ERR_OK;
}
struct Client {
    tcp_pcb* pcb = nullptr; SOCKET socket=INVALID_SOCKET;
    std::vector<uint8_t> to_local;
    bool closed=false,remote_fin=false,local_connected=false;
    ULONGLONG last=GetTickCount64();
};
static std::vector<Client*> clients;
static void fail(Client* client) {
    if(client->closed) return;
    client->closed=true;
    if(client->pcb) { tcp_arg(client->pcb,nullptr); tcp_err(client->pcb,nullptr); tcp_recv(client->pcb,nullptr); tcp_abort(client->pcb); client->pcb=nullptr; }
    if(client->socket!=INVALID_SOCKET) { closesocket(client->socket); client->socket=INVALID_SOCKET; }
}
static void connection_error(void* arg,err_t) {
    auto c=static_cast<Client*>(arg); c->pcb=nullptr; fail(c);
}
static err_t received(void* arg,tcp_pcb*,pbuf* packet,err_t error) {
    auto c=static_cast<Client*>(arg);
    if(error!=ERR_OK) { if(packet) pbuf_free(packet); return error; }
    if(!packet) { c->remote_fin=true; return ERR_OK; }
    if(c->to_local.size()+packet->tot_len>131072) return ERR_MEM;
    size_t before=c->to_local.size(); c->to_local.resize(before+packet->tot_len);
    pbuf_copy_partial(packet,c->to_local.data()+before,packet->tot_len,0);
    pbuf_free(packet); c->last=GetTickCount64(); return ERR_OK;
}
static err_t accepted(void*,tcp_pcb* pcb,err_t) {
    if(clients.size()>=12) { tcp_abort(pcb); return ERR_ABRT; }
    auto c=new Client(); c->pcb=pcb; c->socket=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
    if(c->socket==INVALID_SOCKET) { delete c; tcp_abort(pcb); return ERR_ABRT; }
    u_long nonblock=1; ioctlsocket(c->socket,FIONBIO,&nonblock);
    sockaddr_in target{}; target.sin_family=AF_INET; target.sin_port=htons(18771); target.sin_addr.s_addr=htonl(INADDR_LOOPBACK);
    int status=connect(c->socket,reinterpret_cast<sockaddr*>(&target),sizeof(target));
    if(status==SOCKET_ERROR && WSAGetLastError()!=WSAEWOULDBLOCK) { closesocket(c->socket); delete c; tcp_abort(pcb); return ERR_ABRT; }
    tcp_arg(pcb,c); tcp_recv(pcb,received); tcp_err(pcb,connection_error); tcp_nagle_disable(pcb);
    clients.push_back(c); return ERR_OK;
}
static void poll_clients() {
    uint8_t buffer[16384];
    for(auto c:clients) {
        if(c->closed) continue;
        if(GetTickCount64()-c->last>120000) { fail(c); continue; }
        if(!c->local_connected) {
            fd_set writes,errors; FD_ZERO(&writes); FD_ZERO(&errors); FD_SET(c->socket,&writes); FD_SET(c->socket,&errors); timeval timeout{};
            select(0,nullptr,&writes,&errors,&timeout);
            if(FD_ISSET(c->socket,&errors)) { fail(c); continue; }
            if(!FD_ISSET(c->socket,&writes)) continue;
            int error=0,length=sizeof(error); getsockopt(c->socket,SOL_SOCKET,SO_ERROR,reinterpret_cast<char*>(&error),&length);
            if(error) { fail(c); continue; } c->local_connected=true;
        }
        if(!c->to_local.empty()) {
            int count=send(c->socket,reinterpret_cast<const char*>(c->to_local.data()),static_cast<int>(c->to_local.size()),0);
            if(count>0) { c->to_local.erase(c->to_local.begin(),c->to_local.begin()+count); tcp_recved(c->pcb,static_cast<u16_t>(count)); c->last=GetTickCount64(); }
            else if(count==SOCKET_ERROR && WSAGetLastError()!=WSAEWOULDBLOCK) { fail(c); continue; }
        }
        u16_t available=tcp_sndbuf(c->pcb);
        if(available) {
            int count=recv(c->socket,reinterpret_cast<char*>(buffer),std::min<int>(available,sizeof(buffer)),0);
            if(count>0) {
                if(tcp_write(c->pcb,buffer,static_cast<u16_t>(count),TCP_WRITE_FLAG_COPY)!=ERR_OK) { fail(c); continue; }
                tcp_output(c->pcb); c->last=GetTickCount64();
            } else if(count==0) {
                auto pcb=c->pcb;
                // Detach callbacks before graceful close; lwIP owns the PCB after success.
                tcp_arg(pcb,nullptr); tcp_recv(pcb,nullptr); tcp_err(pcb,nullptr);
                if(tcp_close(pcb)!=ERR_OK) tcp_abort(pcb);
                c->pcb=nullptr; fail(c); continue;
            } else if(WSAGetLastError()!=WSAEWOULDBLOCK) { fail(c); continue; }
        }
        if(c->remote_fin && c->to_local.empty()) { shutdown(c->socket,SD_SEND); c->remote_fin=false; }
    }
    clients.erase(std::remove_if(clients.begin(),clients.end(),[](Client* c){if(!c->closed)return false;delete c;return true;}),clients.end());
}
static void word(std::vector<uint8_t>& b,unsigned v) { b.push_back(static_cast<uint8_t>(v>>8)); b.push_back(static_cast<uint8_t>(v)); }
static void name(std::vector<uint8_t>& b,const std::string& value) {
    size_t start=0;
    while(start<value.size()) { auto end=value.find('.',start); if(end==std::string::npos)end=value.size(); b.push_back(static_cast<uint8_t>(end-start)); b.insert(b.end(),value.begin()+start,value.begin()+end); start=end+1; } b.push_back(0);
}
static std::vector<uint8_t> announcement(const uint8_t address[16],const std::string& id) {
    std::vector<uint8_t> b; word(b,0); word(b,0x8400); word(b,0); word(b,4); word(b,0); word(b,0);
    std::string service="_airdrop._tcp.local", instance=id+"."+service, host=id+".local";
    auto rr=[&](const std::string& owner,unsigned type,const std::vector<uint8_t>& data,bool unique=true) {
        name(b,owner); word(b,type); word(b,unique?0x8001:1); word(b,0); word(b,120); word(b,static_cast<unsigned>(data.size())); b.insert(b.end(),data.begin(),data.end());
    };
    std::vector<uint8_t> data; name(data,instance); rr(service,12,data,false);
    data.clear(); word(data,0); word(data,0); word(data,8771); name(data,host); rr(instance,33,data);
    std::string flags="flags=1019"; data.assign(1,static_cast<uint8_t>(flags.size())); data.insert(data.end(),flags.begin(),flags.end()); rr(instance,16,data);
    data.assign(address,address+16); rr(host,28,data); return b;
}
static udp_pcb* dns=nullptr; static ip_addr_t multicast; static std::vector<uint8_t> dns_packet;
static void announce() {
    auto p=pbuf_alloc(PBUF_TRANSPORT,static_cast<u16_t>(dns_packet.size()),PBUF_RAM);
    if(!p)return; pbuf_take(p,dns_packet.data(),static_cast<u16_t>(dns_packet.size())); udp_sendto(dns,p,&multicast,5353); pbuf_free(p);
}
static void dns_query(void*,udp_pcb*,pbuf* p,const ip_addr_t*,u16_t) {
    uint8_t header[4]{}; if(p->tot_len>=12) { pbuf_copy_partial(p,header,4,0); if(!(header[2]&0x80)) announce(); } pbuf_free(p);
}
int main(int argc,char** argv) {
    WSADATA wsa{}; if(WSAStartup(MAKEWORD(2,2),&wsa))return 1;
    stop_event=CreateEventW(nullptr,TRUE,FALSE,L"Local\\AirDrop.Network.Stop.v1"); ResetEvent(stop_event);
    for(int i=0;i<150;i++) { pipe=CreateFileW(L"\\\\.\\pipe\\AirDrop.Host.v1",GENERIC_READ|GENERIC_WRITE,0,nullptr,OPEN_EXISTING,FILE_FLAG_OVERLAPPED,nullptr); if(pipe!=INVALID_HANDLE_VALUE)break; Sleep(100); }
    if(pipe==INVALID_HANDLE_VALUE) { std::cerr<<"AWDL host pipe unavailable\n"; return 2; }
    uint32_t length=0; uint8_t address[16];
    if(!exact(&length,4,false)||length!=16||!exact(address,16,false))return 3;
    lwip_init(); netif device{}; netif_add_noaddr(&device,nullptr,init_interface,ip6_input);
    ip6_addr_t ip{}; memcpy(ip.addr,address,16); netif_ip6_addr_set(&device,0,&ip); netif_ip6_addr_set_state(&device,0,IP6_ADDR_PREFERRED);
    netif_set_default(&device); netif_set_up(&device); netif_set_link_up(&device);
    auto listener=tcp_new_ip_type(IPADDR_TYPE_V6); tcp_bind(listener,IP_ANY_TYPE,8771); listener=tcp_listen(listener); tcp_accept(listener,accepted);
    dns=udp_new_ip_type(IPADDR_TYPE_V6); udp_bind(dns,IP_ANY_TYPE,5353); udp_recv(dns,dns_query,nullptr);
    ipaddr_aton("ff02::fb",&multicast); mld6_joingroup_netif(&device,ip_2_ip6(&multicast));
    std::string id=argc>1?argv[1]:"airdropwindows"; dns_packet=announcement(address,id);
    std::thread reader([] { while(!stopped) { uint32_t count=0; if(!exact(&count,4,false)||count<40||count>65535)break; std::vector<uint8_t> packet(count); if(!exact(packet.data(),count,false))break; std::lock_guard guard(in_lock); if(incoming.size()<512)incoming.push_back(std::move(packet)); } stopped=true; });
    std::thread writer([] { while(!stopped) { std::vector<uint8_t> packet; {std::lock_guard guard(out_lock);if(!outgoing.empty()){packet=std::move(outgoing.front());outgoing.pop_front();}} if(packet.empty()){Sleep(1);continue;} uint32_t count=static_cast<uint32_t>(packet.size());if(!exact(&count,4,true)||!exact(packet.data(),count,true))break; } stopped=true; });
    std::cout<<"{\"event\":\"network_ready\",\"port\":8771}"<<std::endl;
    ULONGLONG next=0;
    while(!stopped && WaitForSingleObject(stop_event,0)!=WAIT_OBJECT_0) {
        std::deque<std::vector<uint8_t>> packets; {std::lock_guard guard(in_lock);packets.swap(incoming);}
        for(auto& bytes:packets) { auto p=pbuf_alloc(PBUF_RAW,static_cast<u16_t>(bytes.size()),PBUF_RAM);if(p){pbuf_take(p,bytes.data(),static_cast<u16_t>(bytes.size())); if(ip6_input(p,&device)!=ERR_OK)pbuf_free(p);} }
        poll_clients(); sys_check_timeouts();
        if(GetTickCount64()>=next){announce();next=GetTickCount64()+2000;} Sleep(1);
    }
    stopped=true; SetEvent(stop_event); CancelIoEx(pipe,nullptr); reader.join();writer.join();
    for(auto c:clients){fail(c);delete c;} clients.clear();
    CloseHandle(pipe);CloseHandle(stop_event);WSACleanup();return 0;
}
