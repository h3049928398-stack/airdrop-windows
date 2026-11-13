"""AirDrop's local TLS protocol worker. The imported receiver/transfer modules
are independently licensed MIT components from raph559/winawdl, not DropBridge.
Only the loopback user-stack proxy can reach this socket. Consent is per request.
"""
import argparse
import json
import logging
import os
import socket
import ssl
import sys
import threading
import time
import uuid

sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'protocol'))
import receiver

output_lock = threading.Lock()
pending_lock = threading.Lock()
pending = {}


def emit(event, **data):
    with output_lock:
        print(json.dumps({'event': event, **data}, ensure_ascii=False), flush=True)


def controls():
    for line in sys.stdin:
        if len(line) > 8192:
            continue
        try:
            command = json.loads(line)
            with pending_lock:
                request = pending.get(command.get('id'))
                if request:
                    request['accepted'] = command.get('accept') is True
                    request['signal'].set()
        except (ValueError, TypeError):
            continue


class Consent(receiver.Watcher):
    def asked(self, ask, address, left):
        request_id = uuid.uuid4().hex
        request = {'signal': threading.Event(), 'accepted': False}
        with pending_lock:
            pending[request_id] = request
        try:
            files = ask.get('Files') or []
            names = [str(item.get('FileName', '文件'))[:255] for item in files[:512]]
            emit('incoming_request', id=request_id,
                 sender=str(ask.get('SenderComputerName') or '附近的设备')[:255], files=names)
            deadline = time.monotonic() + 45
            while time.monotonic() < deadline:
                if request['signal'].wait(0.25):
                    return request_id if request['accepted'] else None
                if left():
                    break
            return None
        finally:
            with pending_lock:
                pending.pop(request_id, None)
            emit('request_closed', id=request_id)

    def upload_started(self, accepted, tid, total):
        if not accepted:
            return None
        emit('transfer_started', id=accepted, total=total)
        return accepted

    def upload_progress(self, token, done):
        emit('transfer_progress', id=token, bytes=done)

    def stored(self, token, names):
        emit('files_received', id=token, files=names)

    def upload_failed(self, token, reason):
        emit('transfer_failed', id=token, reason=str(reason))


class LoopbackReceiver(receiver.Receiver):
    address_family = socket.AF_INET

    def server_bind(self):
        self.server_address = ('127.0.0.1', self.server_address[1])
        super().server_bind()

    def verify_request(self, request, client_address):
        return client_address[0] == '127.0.0.1'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--name', required=True)
    parser.add_argument('--outdir', required=True)
    parser.add_argument('--keys', required=True)
    options = parser.parse_args()
    logging.basicConfig(stream=sys.stderr, level=logging.INFO)
    os.makedirs(options.outdir, exist_ok=True)
    options.dest = os.path.abspath(options.outdir)
    options.model = 'MacBookPro18,3'
    options.max_receive_percent = 30
    options.trace = options.redact = False
    options.interface_index = 0
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.load_cert_chain(os.path.join(options.keys, 'certificate.self-signed.pem'),
                            os.path.join(options.keys, 'key.self-signed.pem'))
    context.verify_mode = ssl.CERT_NONE
    server = LoopbackReceiver(18771, options, Consent())
    server.socket = context.wrap_socket(server.socket, server_side=True,
                                       do_handshake_on_connect=False)
    threading.Thread(target=controls, daemon=True).start()
    emit('receiver_ready', port=18771)
    try:
        server.serve_forever()
    finally:
        server.reset_sessions()
        server.server_close()


if __name__ == '__main__':
    main()
