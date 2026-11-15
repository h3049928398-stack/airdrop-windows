"""Application-owned Qt tests. No USB, BLE, desktop input, or configuration writes."""
import importlib.util
import json
import os
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
import threading

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, r'D:\AI\wisland')
os.environ.setdefault('QT_QPA_PLATFORM', 'offscreen')
from PySide6 import QtCore, QtWidgets, QtGui
from wisland.core import bg
import wisland.services
import wisland.ui
wisland.services.__path__.insert(0, str(BASE / 'payload/services'))
wisland.ui.__path__.insert(0, str(BASE / 'payload/ui'))
from wisland.services.airdrop import AirDropService
from wisland.ui.airdrop import AirDropPanel
from wisland.ui.theme import theme

app = QtWidgets.QApplication.instance() or QtWidgets.QApplication([])
# The offscreen plugin has no native Windows font database. Explicitly load
# system fonts for meaningful screenshots; the installed GUI uses Windows.
QtGui.QFontDatabase.addApplicationFont('C:/Windows/Fonts/msyh.ttc')
QtGui.QFontDatabase.addApplicationFont('C:/Windows/Fonts/segoeui.ttf')
QtGui.QFontDatabase.addApplicationFont('C:/Windows/Fonts/segmdl2.ttf')
bg.init()


class FakeService(QtCore.QObject):
    changed = QtCore.Signal(dict)
    request_received = QtCore.Signal()
    def __init__(self):
        super().__init__()
        self.state = {'ok': True, 'isRunning': False, 'deviceName': 'ASUS', 'message': '接收已关闭', 'requests': []}
        self.calls = []
    def command(self, command, **values): self.calls.append((command, values))


class ExtensionChecks(unittest.TestCase):
    def setUp(self):
        self.service = FakeService()
        self.page = QtWidgets.QWidget()
        self.page.island = SimpleNamespace(locks=set())
        self.panel = AirDropPanel(self.service, self.page)
    def tearDown(self):
        self.panel.close(); self.page.close(); self.panel.deleteLater(); self.page.deleteLater()
        app.processEvents()
    def test_choices_dispatch_real_control_commands(self):
        self.panel.everyone.click(); self.panel.off.click()
        self.assertEqual(self.service.calls, [('start', {}), ('stop', {})])
    def test_truthful_starting_and_countdown(self):
        self.panel.render({**self.service.state, 'isStarting': True, 'remainingSeconds': 599})
        self.assertFalse(self.panel.everyone.isEnabled())
        self.assertIn('09:59', self.panel.status.text())
        self.assertTrue(self.panel.off.isEnabled())
    def test_request_requires_explicit_id_and_acceptance(self):
        request = {'id': 'local-test', 'sender': '<iPhone>', 'files': ['photo.JPG']}
        self.panel.render({**self.service.state, 'requests': [request]})
        self.panel.accept.click()
        self.assertEqual(self.service.calls, [('respond', {'id': 'local-test', 'accept': True})])
        self.assertEqual(self.panel.request_text.textFormat(), QtCore.Qt.TextFormat.PlainText)
    def test_expired_request_disables_confirmation(self):
        self.panel.render({**self.service.state, 'requests': []})
        self.panel.respond(True)
        self.assertEqual(self.service.calls, [])
    def test_close_unlocks_island_without_stopping_receiving(self):
        self.panel.present(); app.processEvents()
        self.assertIn('airdrop', self.page.island.locks)
        self.panel.close(); app.processEvents()
        self.assertNotIn('airdrop', self.page.island.locks)
        self.assertEqual(self.service.calls, [])
    def test_offline_does_not_claim_receiver_enabled(self):
        self.panel.render({'ok': False, 'message': '未连接'})
        self.assertFalse(self.panel.everyone.isEnabled())
        self.assertTrue(self.panel.off.isChecked())
    def test_progress_and_completion(self):
        self.panel.render({**self.service.state, 'transfer': {'phase': 'receiving', 'bytes': 50, 'total': 100}})
        self.assertEqual(self.panel.progress.value(), 50)
        self.panel.render({**self.service.state, 'transfer': {'phase': 'received', 'lastFile': 'photo.JPG'}})
        self.assertIn('photo.JPG', self.panel.status.text())
    def test_control_stays_responsive_when_shared_pool_is_saturated(self):
        gate = threading.Event()
        barrier = threading.Barrier(9)
        def blocked_job():
            barrier.wait(timeout=3)
            gate.wait(timeout=3)
        jobs = [bg.run_bg(blocked_job) for _ in range(8)]
        barrier.wait(timeout=3)
        service = AirDropService(transport=lambda command: {'ok': True, 'message': 'responsive', 'requests': []})
        loop = QtCore.QEventLoop()
        service.changed.connect(lambda state: loop.quit())
        QtCore.QTimer.singleShot(1000, loop.quit)
        try:
            loop.exec()
            self.assertTrue(service.state.get('ok'))
        finally:
            service.stop(); gate.set()
            for job in jobs: job.result(timeout=4)
    def test_control_page_patch_builds_with_existing_controls(self):
        spec = importlib.util.spec_from_file_location('local_installer', BASE / 'install.py')
        installer = importlib.util.module_from_spec(spec); spec.loader.exec_module(installer)
        original = Path(r'D:\AI\wisland\wisland\ui\pages\system.py').read_bytes()
        if b'Local AirDrop extension' in original:
            backups = sorted(Path(r'D:\AI\wisland\data\extension-backups').glob('airdrop-*/system.py.original'))
            original = backups[-1].read_bytes()
        source = installer.system_source(original)
        module = {'__name__': 'wisland.ui.pages.system', '__package__': 'wisland.ui.pages'}
        exec(compile(source, '<patched control page>', 'exec'), module)
        noop = SimpleNamespace(set_sysproxy=lambda *a: None, set_tun=lambda *a: None)
        ctx = SimpleNamespace(clash=noop)
        page = module['ControlsPage'](ctx)
        theme.set_mode('dark')
        page.setObjectName('testControlSurface')
        page.setStyleSheet('#testControlSurface{background:#242731;}')
        self.assertEqual(page.airdrop.title, '隔空投送')
        page.resize(620, 180); page.show(); app.processEvents()
        self.assertLessEqual(page.airdrop.mapTo(page, QtCore.QPoint(0, page.airdrop.height())).y(), page.rect().height())
        page.grab().save(str(BASE.parents[1] / 'artifacts/wisland-controls.png'))
        page.airdrop_service.stop(); page.close(); page.deleteLater()
    def test_current_theme_layout_renders_without_overflow(self):
        output = BASE.parents[1] / 'artifacts'
        for mode in ('light', 'dark'):
            theme.set_mode(mode)
            self.panel.render(self.service.state); self.panel.show(); app.processEvents()
            self.assertLessEqual(self.panel.height(), 620)
            self.assertTrue(self.panel.rect().contains(self.panel.everyone.geometry()))
            self.panel.grab().save(str(output / f'wisland-airdrop-{mode}.png'))


if __name__ == '__main__':
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ExtensionChecks))
    sys.exit(not result.wasSuccessful())
