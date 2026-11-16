"""AirDrop detail layer embedded in the existing control page/native island."""
from __future__ import annotations
from PySide6 import QtCore, QtGui, QtWidgets
from .theme import theme, ui_font, W
from .control_skin import BLUE, draw_symbol, glass_surface, GlassButton
from .backdrop_filter import blur_frame, composite_widget

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
                      self.overlay.page.island, 1.55,
                      (self.overlay._blurred, self.pos(), self.overlay.size())
                      if not self.overlay._blurred.isNull() else None)
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
        self._original = QtGui.QImage()
        self._blurred = QtGui.QImage()
        self._capturing = False
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
        self.close_button = GlassButton('收起', height=32)
        self.close_button.setAccessibleName('收起隔空投送')
        self.close_button.setFixedWidth(60)
        self.close_button.clicked.connect(self.close)
        header.addWidget(self.close_button)
        layout.addLayout(header)
        self.status = self.label('正在连接…', 11)
        self.status.setWordWrap(True); self.status.setMaximumHeight(58)
        layout.addWidget(self.status)
        self.off = GlassButton('接收关闭', height=44, choice=True)
        self.everyone = GlassButton('所有人，10 分钟', height=44, choice=True)
        self.choices = QtWidgets.QButtonGroup(self)
        self.choices.setExclusive(True)
        for button in (self.off, self.everyone):
            self.choices.addButton(button)
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
        self.decline = GlassButton('拒绝'); self.accept = GlassButton('接收', primary=True)
        self.decline.clicked.connect(lambda: self.respond(False))
        self.accept.clicked.connect(lambda: self.respond(True))
        actions.addWidget(self.decline); actions.addWidget(self.accept)
        request_layout.addLayout(actions); layout.addWidget(self.request_card)
        self.progress = QtWidgets.QProgressBar()
        self.progress.setRange(0, 100); self.progress.setFixedHeight(6); self.progress.setTextVisible(False)
        layout.addWidget(self.progress)
        self.open_folder = GlassButton('打开接收文件夹', height=36)
        self.open_folder.clicked.connect(self.folder)
        layout.addWidget(self.open_folder)
        self.buttons = (self.close_button, self.off, self.everyone, self.decline, self.accept, self.open_folder)
        self.request_card.hide(); self.progress.hide()
        self._animation = QtCore.QVariantAnimation(self, duration=260)
        self._animation.setEasingCurve(QtCore.QEasingCurve.Type.OutCubic)
        self._animation.valueChanged.connect(self._animate_frame)
        self._animation.finished.connect(self._settled)
        self._capture_timer = QtCore.QTimer(self, singleShot=True, interval=160,
                                           timeout=self._capture_backdrop)
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
        self.setStyleSheet(f'QLabel{{color:{fg};background:transparent}}'
                           'QProgressBar{background:rgba(128,128,128,40);border:0;border-radius:3px}'
                           'QProgressBar::chunk{background:#0a9fff;border-radius:3px}')
        for button in self.buttons:
            button.update()
        self.update(); self.card.update()
        self.refresh_backdrop()

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
        rect = QtCore.QRect(max(8, self.page.width() - 308), 8, min(300, self.page.width() - 16), height)
        return rect.translated(self.page.mapTo(self.parentWidget(), QtCore.QPoint()))

    def _origin(self):
        tile = getattr(self.page, 'airdrop', None)
        if tile:
            return QtCore.QRect(tile.mapTo(self.parentWidget(), QtCore.QPoint()), tile.size())
        return QtCore.QRect(max(8, self.page.width() - 140), 8, 130, 118).translated(
            self.page.mapTo(self.parentWidget(), QtCore.QPoint()))

    def _bind_surface(self):
        # Cover the whole expanded content (navigation, gaps and page) in one
        # image. Separate page/nav snapshots leave hard rectangular seams.
        surface = getattr(self.page.island, 'content', None) or self.page
        if self.parentWidget() is not surface:
            if self.parentWidget() is not self.page:
                self.parentWidget().removeEventFilter(self)
            self.setParent(surface, Qt.WindowType.Widget)
            surface.installEventFilter(self)

    def _fit(self):
        self._set_height(max(self._height_before or 0, self._card_height() + 16))
        self.setGeometry(self.parentWidget().rect())
        self.card.setGeometry(self._target())
        self.body.setGeometry(16, 16, 268, self.card.height() - 32)

    def _animate_frame(self, geometry):
        self.card.setGeometry(geometry)
        self.update()

    def refresh_backdrop(self, *_):
        if _ and _[0] is False:  # Existing Backdrop reports an unchanged frame.
            return
        if self._height_before is not None and not self._capturing and not self._capture_timer.isActive():
            self._capture_timer.start()

    def _capture_backdrop(self):
        if self._height_before is None or self._capturing:
            return
        self._capturing = True
        visible = self.isVisible()
        try:
            # Hide only our child overlay during synchronous app-owned render.
            # No event processing occurs here, so the intermediate state is not
            # displayed. This prevents recursive capture of the foreground card.
            self.hide()
            self._original = composite_widget(self.parentWidget(), self.page.island)
            self._blurred = blur_frame(self._original)
        finally:
            if visible:
                self.show(); self.raise_()
            self._capturing = False
        self.update(); self.card.update()

    def present(self):
        island = self.page.island
        if island:
            island.expand('ctl')
        self._bind_surface()
        if self.isVisible():
            if not self._opening:
                self._animation.stop(); self._opening = True; self._fit(); self.body.show()
            self.raise_(); return
        self.render(self.service.state)
        self._height_before = self.page.preferred_height() if hasattr(self.page, 'preferred_height') else self.page.height()
        self._opening = True
        self._set_height(max(self._height_before, self._card_height() + 16))
        self.setGeometry(self.parentWidget().rect()); self.body.hide()
        self._capture_backdrop()
        self.show(); self.raise_()
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
            self.refresh_backdrop()
        else:
            self._capture_timer.stop()
            self.hide()
            self._original = self._blurred = QtGui.QImage()
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
        surface = watched is self.page or watched is self.parentWidget()
        if surface and event.type() == QtCore.QEvent.Type.Hide and self._height_before is not None:
            self.close(immediate=True)
        if surface and event.type() == QtCore.QEvent.Type.Resize and self.isVisible():
            self.setGeometry(self.parentWidget().rect())
            if self._animation.state() == QtCore.QAbstractAnimation.State.Running and self._opening:
                self._animation.setEndValue(self._target())
            self.refresh_backdrop()
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
        page_rect = QtCore.QRect(self.page.mapTo(self.parentWidget(), QtCore.QPoint()), self.page.size())
        nav = getattr(island, 'nav', None)
        report = dict(isVisible=self.isVisible(), isWindow=self.isWindow(),
                      nativeChild=self.testAttribute(Qt.WidgetAttribute.WA_NativeWindow),
                      sharesIslandWindow=bool(island and self.window() is island),
                      parentIsControlPage=self.parentWidget() is self.page,
                      parentIsIslandContent=self.parentWidget() is getattr(island, 'content', None),
                      cardInsidePage=page_rect.contains(self.card.geometry()),
                      animationStopped=self._animation.state() != QtCore.QAbstractAnimation.State.Running,
                      sameWindowTiles=all(tile.window() is self.window() for tile in
                                          (self.page.wifi, self.page.bt, self.page.airdrop)),
                      pageSize=[self.page.width(), self.page.rect().height()],
                      cardGeometry=[self.card.x(), self.card.y(), self.card.width(), self.card.height()],
                      blurCoversPage=QtCore.QRect(QtCore.QPoint(), self._blurred.deviceIndependentSize().toSize()).contains(page_rect),
                      blurCoversSurface=(self._blurred.deviceIndependentSize().toSize() == self.size()),
                      opaqueBlur=(not self._blurred.isNull() and self._blurred.pixelColor(0, 0).alpha() == 255),
                      navBlurVisible=bool(nav and nav.isVisible() and self.isVisible() and
                                          self.rect().contains(QtCore.QRect(nav.mapTo(self.parentWidget(), QtCore.QPoint()), nav.size()))),
                      navSharesWindow=bool(nav and nav.window() is self.window()),
                      buttonsUnified=all(isinstance(button, GlassButton) for button in self.buttons),
                      buttonCount=len(self.buttons),
                      buttonTextFits=all(QtGui.QFontMetrics(button.font()).horizontalAdvance(button.text()) <= button.text_rect().width()
                                         for button in self.buttons if button.isVisible()),
                      scope='application-owned geometry check; no control commands issued by this check')
        target = Path(path); target.parent.mkdir(parents=True, exist_ok=True)
        report['surfaceCaptureSaved'] = self.window().grab().save(str(target.with_suffix('.png')))
        target.write_text(json.dumps(report, indent=2), encoding='utf-8')

    def paint_blur(self, painter, original, blurred, rect):
        fraction = self._animation.currentTime() / max(1, self._animation.duration())
        if self._animation.state() != QtCore.QAbstractAnimation.State.Running:
            fraction = 1 if self._opening else 0
        strength = fraction if self._opening else 1 - fraction
        # During the parent's height animation the next capture can lag by one
        # throttled refresh. Cover the whole new surface until it arrives.
        painter.drawImage(QtCore.QRectF(rect), original)
        painter.setOpacity(strength); painter.drawImage(QtCore.QRectF(rect), blurred)
        painter.fillRect(rect, QtGui.QColor(0, 0, 0, 30 if theme.dark else 12))

    def paintEvent(self, event):
        p = QtGui.QPainter(self)
        island = self.page.island
        if self.parentWidget() is getattr(island, 'content', None) and hasattr(island, 'path'):
            # Preserve the island's rounded silhouette and transparent corners.
            offset = self.parentWidget().mapTo(island, QtCore.QPoint())
            p.setClipPath(island.path.translated(-offset.x(), -offset.y()))
        self.paint_blur(p, self._original, self._blurred, self.rect())
