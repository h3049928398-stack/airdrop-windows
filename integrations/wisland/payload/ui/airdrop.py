"""AirDrop detail layer embedded in the existing control page/native island."""
from __future__ import annotations
from PySide6 import QtCore, QtGui, QtWidgets
from .theme import theme, ui_font, W
from .control_skin import BLUE, draw_symbol, glass_surface

Qt = QtCore.Qt


class AirDropSymbol(QtWidgets.QWidget):
    def __init__(self):
        super().__init__()
        self.setFixedSize(42, 42)
    def paintEvent(self, event):
        p = QtGui.QPainter(self); p.setRenderHint(QtGui.QPainter.RenderHint.Antialiasing)
        p.setPen(Qt.PenStyle.NoPen); p.setBrush(BLUE)
        p.drawEllipse(QtCore.QRectF(self.rect()).adjusted(1, 1, -1, -1))
        draw_symbol(p, 'airdrop', QtCore.QRectF(10, 10, 22, 22))


class GlassCard(QtWidgets.QWidget):
    def __init__(self, overlay):
        super().__init__(overlay)
        self.overlay = overlay
    def paintEvent(self, event):
        p = QtGui.QPainter(self); p.setRenderHint(QtGui.QPainter.RenderHint.Antialiasing)
        glass_surface(p, self, QtCore.QRectF(self.rect()).adjusted(1, 1, -1, -1), 28,
                      self.overlay.page.island, 1.55)
        if not self.overlay.body.isVisible():
            circle = QtCore.QRectF(15, 15, 38, 38)
            p.setPen(Qt.PenStyle.NoPen); p.setBrush(BLUE); p.drawEllipse(circle)
            draw_symbol(p, 'airdrop', circle.adjusted(9, 9, -9, -9))


class AirDropPanel(QtWidgets.QWidget):
    def __init__(self, service, page):
        super().__init__(page, Qt.WindowType.Widget)
        self.service, self.page = service, page
        self.setAccessibleName('隔空投送详情')
        self.setAttribute(Qt.WidgetAttribute.WA_StyledBackground, False)
        self._request_id = None
        self._base = QtGui.QPixmap()
        self._opening = False
        self._height_before = None
        self._had_focus = False
        self.card = GlassCard(self)
        self.body = QtWidgets.QWidget(self.card)
        self.body.setFixedWidth(268)
        layout = QtWidgets.QVBoxLayout(self.body)
        layout.setContentsMargins(0, 0, 0, 0); layout.setSpacing(9)
        header = QtWidgets.QHBoxLayout(); header.setSpacing(10)
        header.addWidget(AirDropSymbol())
        titles = QtWidgets.QVBoxLayout(); titles.setSpacing(2)
        titles.addWidget(self.label('隔空投送', 18, W.DemiBold))
        self.name = self.label('AirDrop', 11)
        titles.addWidget(self.name); header.addLayout(titles, 1)
        self.close_button = QtWidgets.QPushButton('收起')
        self.close_button.setAccessibleName('收起隔空投送')
        self.close_button.setFixedSize(40, 28)
        self.close_button.clicked.connect(self.close)
        header.addWidget(self.close_button)
        layout.addLayout(header)
        self.status = self.label('正在连接…', 11)
        self.status.setWordWrap(True); self.status.setMaximumHeight(58)
        layout.addWidget(self.status)
        self.off = QtWidgets.QPushButton('接收关闭')
        self.everyone = QtWidgets.QPushButton('所有人，10 分钟')
        for button in (self.off, self.everyone):
            button.setMinimumHeight(43); button.setCheckable(True)
            button.setCursor(Qt.CursorShape.PointingHandCursor)
            layout.addWidget(button)
        self.off.clicked.connect(lambda: self.service.command('stop'))
        self.everyone.clicked.connect(lambda: self.service.command('start'))
        self.hint = self.label('附近的 iPhone 可通过分享菜单发送文件。\n每次接收都会征求你的同意。', 10.5)
        self.hint.setWordWrap(True); layout.addWidget(self.hint)
        self.request_card = QtWidgets.QWidget()
        request_layout = QtWidgets.QVBoxLayout(self.request_card)
        request_layout.setContentsMargins(0, 3, 0, 0)
        self.request_text = self.label('', 12)
        self.request_text.setWordWrap(True); self.request_text.setMaximumHeight(100)
        request_layout.addWidget(self.request_text)
        actions = QtWidgets.QHBoxLayout()
        self.decline = QtWidgets.QPushButton('拒绝'); self.accept = QtWidgets.QPushButton('接收')
        self.decline.clicked.connect(lambda: self.respond(False))
        self.accept.clicked.connect(lambda: self.respond(True))
        actions.addWidget(self.decline); actions.addWidget(self.accept)
        request_layout.addLayout(actions); layout.addWidget(self.request_card)
        self.progress = QtWidgets.QProgressBar()
        self.progress.setRange(0, 100); self.progress.setFixedHeight(6); self.progress.setTextVisible(False)
        layout.addWidget(self.progress)
        self.open_folder = QtWidgets.QPushButton('打开接收文件夹')
        self.open_folder.setMinimumHeight(29); self.open_folder.clicked.connect(self.folder)
        layout.addWidget(self.open_folder)
        self.request_card.hide(); self.progress.hide()
        self._animation = QtCore.QVariantAnimation(self, duration=260)
        self._animation.setEasingCurve(QtCore.QEasingCurve.Type.OutCubic)
        self._animation.valueChanged.connect(self.card.setGeometry)
        self._animation.finished.connect(self._settled)
        self.page.installEventFilter(self)
        self._escape = QtGui.QShortcut(QtGui.QKeySequence('Esc'), self)
        self._escape.setContext(Qt.ShortcutContext.WidgetWithChildrenShortcut)
        self._escape.activated.connect(self.close)
        service.changed.connect(self.render)
        service.request_received.connect(self.present)
        theme.changed.connect(self.restyle)
        self.restyle(); self.render(service.state)
        self.hide()

    @staticmethod
    def label(text, size, weight=W.Normal):
        label = QtWidgets.QLabel(text)
        label.setTextFormat(Qt.TextFormat.PlainText); label.setFont(ui_font(size, weight))
        return label

    def restyle(self):
        fg = theme.text().name()
        row = 'rgba(255,255,255,22)' if theme.dark else 'rgba(255,255,255,120)'
        self.setStyleSheet(f'QLabel{{color:{fg};background:transparent}}'
                           f'QPushButton{{color:{fg};background:{row};border:0;border-radius:15px;padding:6px 9px;font-size:12px;text-align:left}}'
                           'QPushButton:checked{background:rgba(10,159,255,190);color:white}'
                           'QPushButton:hover{border:1px solid rgba(255,255,255,100)}'
                           'QPushButton:disabled{color:#888}'
                           'QProgressBar{background:rgba(128,128,128,40);border:0;border-radius:3px}'
                           'QProgressBar::chunk{background:#0a9fff;border-radius:3px}')
        self.update(); self.card.update()

    def render(self, state):
        on, starting = state.get('isRunning', False), state.get('isStarting', False)
        self.name.setText(state.get('deviceName', 'AirDrop'))
        message = state.get('message', '点击控制磁贴重新连接')
        remaining = state.get('remainingSeconds')
        if remaining is not None and (on or starting):
            message += f'\n剩余 {remaining // 60:02d}:{remaining % 60:02d}'
        self.status.setText(message); self.status.setToolTip(message)
        self.off.setChecked(not on and not starting); self.everyone.setChecked(on or starting)
        self.everyone.setText('正在准备接收…' if starting else '所有人，10 分钟')
        self.everyone.setEnabled(state.get('ok', False) and not starting)
        self.off.setEnabled(state.get('ok', False))
        requests = state.get('requests') or []
        self._request_id = requests[0]['id'] if requests else None
        self.request_card.setVisible(bool(requests))
        if requests:
            request = requests[0]
            full = '来自 ' + request.get('sender', '附近的设备') + '\n' + '\n'.join(request.get('files', [])[:6])
            self.request_text.setText(full[:420]); self.request_text.setToolTip(full)
        self.accept.setEnabled(bool(requests)); self.decline.setEnabled(bool(requests))
        transfer = state.get('transfer') or {}; phase = transfer.get('phase')
        self.progress.setVisible(phase == 'receiving')
        if phase == 'receiving':
            total = transfer.get('total', 0)
            self.progress.setRange(0, 100 if total else 0)
            self.progress.setValue(min(99, int(transfer.get('bytes', 0) * 100 / max(1, total))))
        if phase == 'received':
            self.status.setText('接收完成\n' + transfer.get('lastFile', '文件已保存'))
        self.open_folder.setEnabled(bool(state.get('receiveDirectory')))
        if self.isVisible() and self._animation.state() != QtCore.QAbstractAnimation.State.Running:
            self._fit()

    def _card_height(self):
        return min(510, max(300, self.body.layout().totalHeightForWidth(268) + 32,
                            self.body.sizeHint().height() + 32))

    def _set_height(self, height):
        if hasattr(self.page, 'set_height'):
            self.page.set_height(height)
        else:
            self.page.resize(self.page.width(), height)

    def _target(self):
        height = self._card_height()
        return QtCore.QRect(max(8, self.page.width() - 308), 8, min(300, self.page.width() - 16), height)

    def _origin(self):
        tile = getattr(self.page, 'airdrop', None)
        if tile:
            return QtCore.QRect(tile.mapTo(self.page, QtCore.QPoint()), tile.size())
        return QtCore.QRect(max(8, self.page.width() - 140), 8, 130, 118)

    def _fit(self):
        self._set_height(max(self._height_before or 0, self._card_height() + 16))
        self.setGeometry(self.page.rect())
        self.card.setGeometry(self._target())
        self.body.setGeometry(16, 16, 268, self.card.height() - 32)

    def present(self):
        island = self.page.island
        if island:
            island.expand('ctl')
        if self.isVisible():
            if not self._opening:
                self._animation.stop(); self._opening = True; self._fit(); self.body.show()
            self.raise_(); return
        self.render(self.service.state)
        self._height_before = self.page.preferred_height() if hasattr(self.page, 'preferred_height') else self.page.height()
        self._base = self.page.grab()
        if not self._base.isNull():
            size = self._base.size()
            self._base = self._base.scaled(max(1, size.width() // 14), max(1, size.height() // 14), Qt.AspectRatioMode.IgnoreAspectRatio, Qt.TransformationMode.SmoothTransformation).scaled(size, Qt.AspectRatioMode.IgnoreAspectRatio, Qt.TransformationMode.SmoothTransformation)
        self._opening = True
        self._set_height(max(self._height_before, self._card_height() + 16))
        self.setGeometry(self.page.rect()); self.body.hide(); self.show(); self.raise_()
        if island:
            island.locks.add('airdrop')
            self._had_focus = 'focus' in island.locks
            if hasattr(island, 'request_focus') and not self._had_focus:
                island.request_focus()
        self._animation.stop()
        self._animation.setStartValue(self._origin()); self._animation.setEndValue(self._target())
        self._animation.start()

    def _settled(self):
        if self._opening:
            self._fit(); self.body.show()
        else:
            self.hide()
            if self._height_before is not None:
                self._set_height(self._height_before); self._height_before = None
            island = self.page.island
            if island:
                island.locks.discard('airdrop')
                if hasattr(island, 'release_focus') and not self._had_focus:
                    island.release_focus()

    def close(self, immediate=False):
        if self._height_before is None:
            return True
        self._opening = False; self.body.hide(); self._animation.stop()
        if immediate:
            self._settled()
        else:
            self._animation.setStartValue(self.card.geometry()); self._animation.setEndValue(self._origin())
            self._animation.start()
        return True

    def eventFilter(self, watched, event):
        if watched is self.page and event.type() == QtCore.QEvent.Type.Hide and self._height_before is not None:
            self.close(immediate=True)
        if watched is self.page and event.type() == QtCore.QEvent.Type.Resize and self.isVisible():
            self.setGeometry(self.page.rect())
            if self._animation.state() == QtCore.QAbstractAnimation.State.Running and self._opening:
                self._animation.setEndValue(self._target())
        return super().eventFilter(watched, event)

    def mousePressEvent(self, event):
        if not self.card.geometry().contains(event.position().toPoint()):
            self.close()
        event.accept()

    def respond(self, accept):
        if self._request_id:
            self.accept.setEnabled(False); self.decline.setEnabled(False)
            self.service.command('respond', id=self._request_id, accept=accept)

    def folder(self):
        directory = self.service.state.get('receiveDirectory')
        if directory:
            QtGui.QDesktopServices.openUrl(QtCore.QUrl.fromLocalFile(directory))

    def write_layout_report(self, path):
        """Opt-in application-owned geometry QA; no external window inspection."""
        import json
        from pathlib import Path
        island = self.page.island
        report = dict(isVisible=self.isVisible(), isWindow=self.isWindow(),
                      nativeChild=self.testAttribute(Qt.WidgetAttribute.WA_NativeWindow),
                      sharesIslandWindow=bool(island and self.window() is island),
                      parentIsControlPage=self.parentWidget() is self.page,
                      cardInsidePage=self.page.rect().contains(self.card.geometry()),
                      animationStopped=self._animation.state() != QtCore.QAbstractAnimation.State.Running,
                      sameWindowTiles=all(tile.window() is self.window() for tile in
                                          (self.page.wifi, self.page.bt, self.page.airdrop)),
                      pageSize=[self.page.width(), self.page.rect().height()],
                      cardGeometry=[self.card.x(), self.card.y(), self.card.width(), self.card.height()],
                      scope='application-owned geometry check; no control commands issued by this check')
        target = Path(path); target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(report, indent=2), encoding='utf-8')

    def paintEvent(self, event):
        p = QtGui.QPainter(self)
        if not self._base.isNull():
            p.drawPixmap(0, 0, self._base)
        p.fillRect(self.rect(), QtGui.QColor(0, 0, 0, 30 if theme.dark else 12))
