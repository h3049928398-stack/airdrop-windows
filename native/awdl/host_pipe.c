/* SPDX-License-Identifier: GPL-3.0-or-later
 * Original user-space host port implementing the independent WinAWDL host_if
 * contract. No Wintun device, system IP configuration or firewall changes. */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "host_if.h"
#include "../common/pipe_io.h"
#define SLOTS 128
struct slot { int length; uint8_t bytes[HOST_IF_MAX_PACKET]; };
struct host_if {
    HANDLE pipe,thread,event;
    CRITICAL_SECTION lock,write_lock;
    struct slot *slots;
    unsigned head,count;
    volatile LONG closing;
};
static int exact(HANDLE pipe,void *buffer,DWORD length,int writing) {
    return air_pipe_exact(pipe,buffer,length,writing,NULL);
}
static DWORD WINAPI read_host(void *pointer) {
    struct host_if *host = pointer;
    uint8_t *packet = malloc(HOST_IF_MAX_PACKET);
    uint32_t length;
    while(packet && !host->closing && exact(host->pipe,&length,4,0)) {
        if(length < 40 || length > HOST_IF_MAX_PACKET || !exact(host->pipe,packet,length,0)) break;
        EnterCriticalSection(&host->lock);
        if(host->count < SLOTS) {
            struct slot *slot = &host->slots[(host->head+host->count)%SLOTS];
            slot->length = (int)length; memcpy(slot->bytes,packet,length); host->count++;
            SetEvent(host->event);
        }
        LeaveCriticalSection(&host->lock);
    }
    free(packet); return 0;
}
struct host_if *host_if_open(const uint8_t address[16],char *error,size_t error_len) {
    struct host_if *host = calloc(1,sizeof(*host));
    if(!host) return NULL;
    InitializeCriticalSection(&host->lock); InitializeCriticalSection(&host->write_lock);
    host->event = CreateEventW(NULL,TRUE,FALSE,NULL);
    host->slots = calloc(SLOTS,sizeof(struct slot));
    host->pipe = CreateNamedPipeW(L"\\\\.\\pipe\\AirDrop.Host.v1",PIPE_ACCESS_DUPLEX|FILE_FLAG_OVERLAPPED,
        PIPE_TYPE_BYTE|PIPE_READMODE_BYTE|PIPE_WAIT|PIPE_REJECT_REMOTE_CLIENTS,1,262144,262144,0,NULL);
    if(host->pipe == INVALID_HANDLE_VALUE || !host->slots) {
        snprintf(error,error_len,"host pipe allocation failed (%lu)",GetLastError()); host_if_close(host); return NULL;
    }
    /* The stack opens this pipe before scheduling can begin. */
    if(!air_pipe_connect(host->pipe,NULL)) {
        snprintf(error,error_len,"host pipe connection failed"); host_if_close(host); return NULL;
    }
    uint32_t length = 16;
    if(!exact(host->pipe,&length,4,1) || !exact(host->pipe,(void*)address,16,1)) { host_if_close(host); return NULL; }
    host->thread = CreateThread(NULL,0,read_host,host,0,NULL);
    if(!host->thread) { host_if_close(host); return NULL; }
    fprintf(stdout,"host pipe ready\n"); fflush(stdout);
    return host;
}
void host_if_close(struct host_if *host) {
    if(!host) return;
    host->closing = 1;
    if(host->thread) { CancelIoEx(host->pipe,NULL); WaitForSingleObject(host->thread,INFINITE); CloseHandle(host->thread); }
    if(host->pipe && host->pipe != INVALID_HANDLE_VALUE) { DisconnectNamedPipe(host->pipe); CloseHandle(host->pipe); }
    if(host->event) CloseHandle(host->event);
    DeleteCriticalSection(&host->lock); DeleteCriticalSection(&host->write_lock);
    free(host->slots); free(host);
}
void *host_if_read_event(const struct host_if *host) { return host->event; }
unsigned long host_if_index(const struct host_if *host) { (void)host; return 0; }
int host_if_read(struct host_if *host,uint8_t *packet) {
    int length = 0;
    EnterCriticalSection(&host->lock);
    if(host->count) {
        struct slot *slot = &host->slots[host->head]; length = slot->length; memcpy(packet,slot->bytes,length);
        host->head = (host->head+1)%SLOTS; host->count--;
    } else ResetEvent(host->event);
    LeaveCriticalSection(&host->lock); return length;
}
int host_if_write(struct host_if *host,const uint8_t *packet,int len) {
    if(len < 40 || len > HOST_IF_MAX_PACKET) return -1;
    uint32_t length = (uint32_t)len;
    EnterCriticalSection(&host->write_lock);
    int ok = exact(host->pipe,&length,4,1) && exact(host->pipe,(void*)packet,length,1);
    LeaveCriticalSection(&host->write_lock);
    return ok ? 0 : -1;
}
