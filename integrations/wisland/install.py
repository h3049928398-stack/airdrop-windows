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
        ('        self._radio = {}\n', '''        # Local AirDrop extension (original sources in AirDrop-Windows).
        from ...services.airdrop import AirDropService
        from ..airdrop import AirDropPanel
        self.airdrop_service = AirDropService(self)
        self.airdrop_panel = AirDropPanel(self.airdrop_service, self)
        self.airdrop = Tile("\\uE92D", "隔空投送")
        self.airdrop.setAccessibleName("隔空投送")
        self.airdrop.clicked.connect(self._open_airdrop)
        self.airdrop_service.changed.connect(self._airdrop_state)
        if "--airdrop" in QtCore.QCoreApplication.arguments():
            QtCore.QTimer.singleShot(600, self._open_airdrop)
        self._radio = {}
'''),
        ('            grid.addWidget(t, i // 2, i % 2)\n', '            grid.addWidget(t, i // 2, i % 2)\n        grid.addWidget(self.airdrop, 2, 0, 1, 2)\n'),
        ('    def _toggle(self, kind: str):\n', '''    def _open_airdrop(self):
        self.airdrop_panel.present()
        self.airdrop_service.connect_backend()

    def _airdrop_state(self, state):
        on = state.get("isRunning", False)
        starting = state.get("isStarting", False)
        self.airdrop.set_state(on or starting,
                              "正在准备…" if starting else "接收已开启" if on else "接收关闭 · 点击设置")

    def _toggle(self, kind: str):
'''),
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
    desired = {
        system: system_source(system.read_bytes()),
        target / 'wisland/services/airdrop.py': (BASE / 'payload/services/airdrop.py').read_bytes(),
        target / 'wisland/ui/airdrop.py': (BASE / 'payload/ui/airdrop.py').read_bytes(),
        target / 'docs/AIRDROP_EXTENSION.md': (BASE / 'README.md').read_bytes(),
    }
    for path in desired:
        if path != system and path.exists():
            raise RuntimeError('扩展文件已存在，停止以避免覆盖：' + str(path))
    backup = target / 'data/extension-backups' / ('airdrop-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir(parents=True, exist_ok=False)
    shutil.copy2(system, backup / 'system.py.original')
    manifest = []
    for path, content in desired.items():
        path.write_bytes(content)
        manifest.append({'path': str(path.relative_to(target)), 'sha256': hashlib.sha256(content).hexdigest(), 'created': path != system})
    (backup / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'installed': [x['path'] for x in manifest], 'backup': str(backup)}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('target', type=Path)
    install(parser.parse_args().target)
