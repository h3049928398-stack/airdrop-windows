"""Application-owned Qt tests. No USB, BLE, desktop input, or configuration writes."""
import importlib.util
import json
import os
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
import threading
import tempfile

BASE = Path(__file__).resolve().parents[1]
sys.path.insert(0, r'D:\AI\wisland')
os.environ.setdefault('QT_QPA_PLATFORM', 'offscreen')
from PySide6 import QtCore, QtWidgets, QtGui, QtTest
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


class ControlSurface(QtWidgets.QWidget):
    def __init__(self):
        super().__init__()
        self.island = SimpleNamespace(locks=set(), expand=lambda page: None)
        self._height = 220
        self.resize(620, 220)
        self.setObjectName('surface')
        self.setStyleSheet('#surface{background:#242731}')
    def preferred_height(self): return self._height
    def set_height(self, height):
        self._height = height
        self.resize(self.width(), height)


class ExtensionChecks(unittest.TestCase):
    def setUp(self):
        self.service = FakeService()
        self.page = ControlSurface()
        self.panel = AirDropPanel(self.service, self.page)
        self.page.show()
    def tearDown(self):
        self.panel.close(immediate=True); self.page.close(); self.panel.deleteLater(); self.page.deleteLater()
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
        self.panel.close(); QtTest.QTest.qWait(320)
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
            original = next(p.read_bytes() for p in backups if installer.hashlib.sha256(p.read_bytes()).hexdigest() == installer.ORIGINAL_SYSTEM_SHA256)
        source = installer.system_source(original)
        module = {'__name__': 'wisland.ui.pages.system', '__package__': 'wisland.ui.pages'}
        exec(compile(source, '<patched control page>', 'exec'), module)
        noop = SimpleNamespace(set_sysproxy=lambda *a: None, set_tun=lambda *a: None)
        ctx = SimpleNamespace(clash=noop)
        page = module['ControlsPage'](ctx)
        page.airdrop_service.stop()
        # These are layout fixtures, not hardware/transfer evidence.
        page.airdrop_service.state = dict(self.service.state)
        page._airdrop_state(page.airdrop_service.state)
        page.wifi.set_state(True, '已开启')
        page.bt.set_state(True, '打开')
        theme.set_mode('dark')
        page.setObjectName('testControlSurface')
        page.setStyleSheet('#testControlSurface{background:#242731;}')
        self.assertEqual(page.airdrop.title, '隔空投送')
        page.resize(620, 220); page.show(); app.processEvents()
        self.assertLessEqual(page.airdrop.mapTo(page, QtCore.QPoint(0, page.airdrop.height())).y(), page.rect().height())
        page.grab().save(str(BASE.parents[1] / 'artifacts/wisland-controls.png'))
        page.island = SimpleNamespace(locks=set(), expand=lambda p: None, page_size_changed=lambda: page.resize(620, page.preferred_height()))
        page.airdrop_panel.present(); QtTest.QTest.qWait(330)
        self.assertFalse(page.airdrop_panel.isWindow())
        self.assertIs(page.airdrop_panel.window(), page.window())
        self.assertTrue(page.airdrop_panel.rect().contains(page.airdrop_panel.card.geometry()))
        page.grab().save(str(BASE.parents[1] / 'artifacts/wisland-controls-expanded.png'))
        page.airdrop_panel.close(immediate=True); page.close(); page.deleteLater()
    def test_current_theme_layout_renders_without_overflow(self):
        output = BASE.parents[1] / 'artifacts'
        for mode in ('light', 'dark'):
            theme.set_mode(mode)
            self.panel.render(self.service.state); self.panel.present(); QtTest.QTest.qWait(330)
            self.assertLessEqual(self.panel.height(), 620)
            self.assertFalse(self.panel.isWindow())
            self.assertTrue(self.panel.rect().contains(self.panel.card.geometry()))
            self.page.grab().save(str(output / f'wisland-airdrop-{mode}.png'))
            self.panel.close(immediate=True)
    def test_click_outside_and_escape_close_with_no_radio_command(self):
        for escape in (False, True):
            self.panel.present(); QtTest.QTest.qWait(300)
            if escape:
                self.panel.setFocus()
                QtTest.QTest.keyClick(self.panel, QtCore.Qt.Key.Key_Escape)
            else:
                QtTest.QTest.mouseClick(self.panel, QtCore.Qt.MouseButton.LeftButton, pos=QtCore.QPoint(12, 12))
            QtTest.QTest.qWait(320)
            self.assertFalse(self.panel.isVisible())
            self.assertEqual(self.page.preferred_height(), 220)
        self.assertEqual(self.service.calls, [])
    def test_reopening_does_not_create_any_secondary_window(self):
        for _ in range(3):
            self.panel.present(); QtTest.QTest.qWait(300)
            self.assertIs(self.panel.window(), self.page)
            self.assertNotIn(self.panel, app.topLevelWidgets())
            self.panel.close(immediate=True)
        self.assertEqual(self.page.island.locks, set())
    def test_parent_hiding_cleans_up_overlay_and_restores_page(self):
        self.panel.present(); QtTest.QTest.qWait(300)
        self.page.hide(); app.processEvents()
        self.assertEqual(self.page.preferred_height(), 220)
        self.assertNotIn('airdrop', self.page.island.locks)
        self.assertIsNone(self.panel._height_before)
    def test_request_during_close_reopens_same_embedded_card(self):
        self.panel.present(); QtTest.QTest.qWait(300)
        self.panel.close()
        self.panel.present(); QtTest.QTest.qWait(300)
        self.assertTrue(self.panel.isVisible())
        self.assertTrue(self.panel.body.isVisible())
        self.assertFalse(self.panel.isWindow())
        self.assertEqual(self.service.calls, [])
    def test_installer_refuses_unknown_existing_file_without_partial_write(self):
        spec = importlib.util.spec_from_file_location('guard_installer', BASE / 'install.py')
        installer = importlib.util.module_from_spec(spec); spec.loader.exec_module(installer)
        backups = sorted(Path(r'D:\AI\wisland\data\extension-backups').glob('airdrop-*/system.py.original'))
        original = next(p.read_bytes() for p in backups if installer.hashlib.sha256(p.read_bytes()).hexdigest() == installer.ORIGINAL_SYSTEM_SHA256)
        with tempfile.TemporaryDirectory(prefix='wisland-install-') as directory:
            root = Path(directory)
            system = root / 'wisland/ui/pages/system.py'
            system.parent.mkdir(parents=True); system.write_bytes(original)
            unknown = root / 'wisland/ui/airdrop.py'; unknown.write_bytes(b'user-local-module')
            with self.assertRaises(RuntimeError): installer.install(root)
            self.assertEqual(system.read_bytes(), original)
            self.assertEqual(unknown.read_bytes(), b'user-local-module')
            self.assertFalse((root / 'data').exists())


if __name__ == '__main__':
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(ExtensionChecks))
    sys.exit(not result.wasSuccessful())
