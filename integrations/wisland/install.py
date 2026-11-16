"""Install the local extension with hash guards and backups; no config secrets copied."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import datetime

BASE = Path(__file__).resolve().parent
ORIGINAL_SYSTEM_SHA256 = '2920592a75af1471c7b3c49c88f36a5a27a0d387e0130c3da53c846780b7b368'


def system_source(original: bytes) -> bytes:
    if hashlib.sha256(original).hexdigest() != ORIGINAL_SYSTEM_SHA256:
        raise RuntimeError('控制页与已检查版本不同，停止安装以保留本地改动。')
    text = original.decode('utf-8').replace('\r\n', '\n')
    edits = [
        ('from .base import Page\n', 'from .base import Page\nfrom ..control_skin import ConnectionTile\n'),
        ('"控制", 180\n', '"控制", 220\n'),
        ('self.wifi = Tile(G.WIFI, "WLAN")', 'self.wifi = ConnectionTile("wifi", "无线局域网")'),
        ('self.bt = Tile(G.BT, "蓝牙")', 'self.bt = ConnectionTile("bt", "蓝牙", compact=True)'),
        ('        self._radio = {}\n', '''        # Local AirDrop extension (original sources in AirDrop-Windows).
        from ...services.airdrop import AirDropService
        from ..airdrop import AirDropPanel
        self.airdrop_service = AirDropService(self)
        self.airdrop_panel = AirDropPanel(self.airdrop_service, self)
        self.airdrop = ConnectionTile("airdrop", "隔空投送")
        self.airdrop.setAccessibleName("隔空投送")
        self.airdrop.clicked.connect(self._open_airdrop)
        self.airdrop_service.changed.connect(self._airdrop_state)
        if "--airdrop" in QtCore.QCoreApplication.arguments():
            QtCore.QTimer.singleShot(600, self._open_airdrop)
        for argument in QtCore.QCoreApplication.arguments():
            if argument.startswith("--airdrop-layout-report="):
                path = argument.split("=", 1)[1]
                QtCore.QTimer.singleShot(3000, lambda path=path: self.airdrop_panel.write_layout_report(path))
        self._radio = {}
'''),
        ('        for i, t in enumerate((self.wifi, self.bt, self.proxy, self.tun)):\n            grid.addWidget(t, i // 2, i % 2)\n', '''        grid.addWidget(self.wifi, 0, 0)
        grid.addWidget(self.airdrop, 0, 1)
        grid.addWidget(self.bt, 1, 0, 1, 2)
        grid.addWidget(self.proxy, 2, 0)
        grid.addWidget(self.tun, 2, 1)
'''),
        ('vbox(None, gw, None)', 'vbox(gw, None)'),
        ('    def _toggle(self, kind: str):\n', '''    def _open_airdrop(self):
        for tile in (self.wifi, self.bt, self.airdrop):
            tile.island = self.island
        self.airdrop_panel.present()
        self.airdrop_service.connect_backend()

    def _airdrop_state(self, state):
        on = state.get("isRunning", False)
        starting = state.get("isStarting", False)
        self.airdrop.set_state(on or starting,
                              "正在准备…" if starting else "所有人，10分钟" if on else "接收关闭")

    def _toggle(self, kind: str):
'''),
        ('        ok = d.get("ok")\n', '''        ok = d.get("ok")
        height = 280 if ok else 220
        if self.airdrop_panel.isVisible():
            self.airdrop_panel._height_before = height
        else:
            self.set_height(height)
'''),
        ('    def on_show(self):\n        super().on_show()\n        self.ctx.brightness.refresh()', '''    def on_hide(self):
        self.airdrop_panel.close(immediate=True)
        super().on_hide()

    def on_show(self):
        super().on_show()
        for tile in (self.wifi, self.bt, self.airdrop):
            tile.island = self.island
        if self.island and not getattr(self, '_glass_hooked', False):
            self.island.backdrop.ready.connect(self.airdrop.update)
            self.island.backdrop.ready.connect(self.wifi.update)
            self.island.backdrop.ready.connect(self.bt.update)
            self.island.backdrop.ready.connect(self.airdrop_panel.card.update)
            self._glass_hooked = True
        self.ctx.brightness.refresh()'''),
        ('        self.ctx.radio.refresh()\n', '        self.ctx.radio.refresh()\n        self.airdrop_service.refresh()\n'),
    ]
    for before, after in edits:
        if text.count(before) != 1:
            raise RuntimeError('控制页插入点不唯一，停止安装。')
        text = text.replace(before, after)
    return text.encode('utf-8')


def install(target: Path):
    target = target.resolve()
    if not (target / 'wisland' / 'ui' / 'pages' / 'system.py').is_file():
        raise RuntimeError('目标不是 wisland 安装目录。')
    system = target / 'wisland/ui/pages/system.py'
    current = system.read_bytes()
    current_manifest = None
    if hashlib.sha256(current).hexdigest() != ORIGINAL_SYSTEM_SHA256:
        manifests = sorted((target / 'data/extension-backups').glob('airdrop-*/manifest.json'), reverse=True)
        for manifest_file in manifests:
            entries = json.loads(manifest_file.read_text(encoding='utf-8'))
            entry = next((x for x in entries if x['path'].replace('\\', '/') == 'wisland/ui/pages/system.py'), None)
            if entry and entry['sha256'] == hashlib.sha256(current).hexdigest():
                current_manifest = entries
                break
        if current_manifest is None:
            raise RuntimeError('控制页有未知改动，停止升级。')
        originals = sorted((target / 'data/extension-backups').glob('airdrop-*/system.py.original'))
        current = next((p.read_bytes() for p in originals if hashlib.sha256(p.read_bytes()).hexdigest() == ORIGINAL_SYSTEM_SHA256), b'')
    desired = {
        system: system_source(current),
        target / 'wisland/services/airdrop.py': (BASE / 'payload/services/airdrop.py').read_bytes(),
        target / 'wisland/ui/airdrop.py': (BASE / 'payload/ui/airdrop.py').read_bytes(),
        target / 'wisland/ui/control_skin.py': (BASE / 'payload/ui/control_skin.py').read_bytes(),
        target / 'docs/AIRDROP_EXTENSION.md': (BASE / 'README.md').read_bytes(),
    }
    for path in desired:
        if path != system and path.exists():
            entry = next((x for x in (current_manifest or []) if x['path'].replace('\\', '/') == path.relative_to(target).as_posix()), None)
            if not entry or entry['sha256'] != hashlib.sha256(path.read_bytes()).hexdigest():
                raise RuntimeError('扩展文件有未知改动，停止以避免覆盖：' + str(path))
    backup = target / 'data/extension-backups' / ('airdrop-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir(parents=True, exist_ok=False)
    shutil.copy2(system, backup / 'system.py.original')
    manifest = []
    for path, content in desired.items():
        existed = path.exists()
        saved = backup / 'previous' / path.relative_to(target)
        if existed:
            saved.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, saved)
        path.write_bytes(content)
        manifest.append({'path': str(path.relative_to(target)), 'sha256': hashlib.sha256(content).hexdigest(), 'created': not existed,
                         'backup': str(saved.relative_to(backup)) if existed else None})
    (backup / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'installed': [x['path'] for x in manifest], 'backup': str(backup)}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('target', type=Path)
    install(parser.parse_args().target)
