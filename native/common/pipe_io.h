/* SPDX-License-Identifier: MIT
 * Original cancellable full-duplex pipe IO. Synchronous Windows file handles
 * serialize ReadFile/WriteFile and can deadlock an idle reader with a writer. */
#pragma once
#include <windows.h>
static int air_pipe_complete(HANDLE pipe, OVERLAPPED *op, HANDLE stop, DWORD *count) {
    HANDLE waits[2] = { op->hEvent, stop };
    DWORD result = WaitForMultipleObjects(stop ? 2 : 1, waits, FALSE, INFINITE);
    if (result != WAIT_OBJECT_0) {
        CancelIoEx(pipe, op);
        GetOverlappedResult(pipe, op, count, TRUE);
        return 0;
    }
    return GetOverlappedResult(pipe, op, count, FALSE) != 0;
}
static int air_pipe_exact(HANDLE pipe, void *buffer, DWORD length, int writing, HANDLE stop) {
    unsigned char *bytes = (unsigned char *)buffer;
    OVERLAPPED op = {0};
    op.hEvent = CreateEventW(NULL, TRUE, FALSE, NULL);
    if (!op.hEvent) return 0;
    int success = 1;
    while (length) {
        if (stop && WaitForSingleObject(stop,0) == WAIT_OBJECT_0) { success=0; break; }
        DWORD count = 0; ResetEvent(op.hEvent);
        BOOL ok = writing ? WriteFile(pipe,bytes,length,&count,&op) : ReadFile(pipe,bytes,length,&count,&op);
        if (!ok && (GetLastError()!=ERROR_IO_PENDING || !air_pipe_complete(pipe,&op,stop,&count))) { success=0; break; }
        if (!count) { success=0; break; }
        bytes += count; length -= count;
    }
    CloseHandle(op.hEvent); return success;
}
static int air_pipe_connect(HANDLE pipe, HANDLE stop) {
    OVERLAPPED op = {0}; DWORD count=0;
    op.hEvent = CreateEventW(NULL,TRUE,FALSE,NULL);
    if (!op.hEvent) return 0;
    BOOL ok = ConnectNamedPipe(pipe,&op);
    if (!ok) {
        DWORD error=GetLastError();
        ok = error==ERROR_PIPE_CONNECTED || (error==ERROR_IO_PENDING && air_pipe_complete(pipe,&op,stop,&count));
    }
    CloseHandle(op.hEvent); return ok != 0;
}
