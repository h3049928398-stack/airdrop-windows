"""Phone-style AirDrop receiving sheet; shared wisland theme, no desktop capture."""
from __future__ import annotations
from PySide6 import QtCore, QtGui, QtWidgets
from .theme import theme, ui_font, W

Qt = QtCore.Qt


class AirDropSymbol(QtWidgets.QWidget):
    def __init__(self):
        super().__init__()
        self.setFixedSize(90, 90)
        theme.changed.connect(self.update)

    def paintEvent(self, event):
        p = QtGui.QPainter(self)
        p.setRenderHint(QtGui.QPainter.RenderHint.Antialiasing)
        color = QtGui.QColor('#4386ff')
        p.setBrush(Qt.BrushStyle.NoBrush)
        for radius, alpha in ((38, 40), (29, 85), (20, 210)):
            color.setAlpha(alpha)
            p.setPen(QtGui.QPen(color, 1.6))
            p.drawEllipse(QtCore.QPointF(45, 41), radius, radius)
        p.setPen(Qt.PenStyle.NoPen)
        p.setBrush(QtGui.QColor('#4386ff'))
        p.drawPolygon(QtGui.QPolygonF([QtCore.QPointF(45, 35), QtCore.QPointF(30, 60), QtCore.QPointF(60, 60)]))


class AirDropPanel(QtWidgets.QWidget):
    def __init__(self, service, page):
        super().__init__(page, Qt.WindowType.Dialog | Qt.WindowType.FramelessWindowHint | Qt.WindowType.WindowStaysOnTopHint)
        self.service, self.page = service, page
        self.setWindowTitle('隔空投送')
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)
        self.setFixedWidth(350)
        self._request_id = None
        self._drag_offset = None
        layout = QtWidgets.QVBoxLayout(self)
        layout.setContentsMargins(22, 18, 22, 22)
        layout.setSpacing(12)
        title = self.label('隔空投送', 20, W.DemiBold)
        self.close_button = QtWidgets.QPushButton('×')
        self.close_button.setFixedSize(28, 28)
        self.close_button.clicked.connect(self.close)
        header = QtWidgets.QHBoxLayout()
        header.addWidget(title); header.addStretch(); header.addWidget(self.close_button)
        layout.addLayout(header)
        layout.addWidget(AirDropSymbol(), alignment=Qt.AlignmentFlag.AlignHCenter)
        self.name = self.label('AirDrop', 18, W.DemiBold, center=True)
        layout.addWidget(self.name)
        self.status = self.label('正在连接…', 12, center=True)
        self.status.setWordWrap(True)
        layout.addWidget(self.status)
        self.off = QtWidgets.QPushButton('接收关闭')
        self.everyone = QtWidgets.QPushButton('所有人，10 分钟')
        for button in (self.off, self.everyone):
            button.setMinimumHeight(46)
            button.setCursor(Qt.CursorShape.PointingHandCursor)
            button.setCheckable(True)
            layout.addWidget(button)
        self.off.clicked.connect(lambda: self.service.command('stop'))
        self.everyone.clicked.connect(lambda: self.service.command('start'))
        self.hint = self.label('附近的 iPhone 可通过分享菜单发送文件。\n每次接收都会征求你的同意。', 11, center=True)
        self.hint.setWordWrap(True)
        layout.addWidget(self.hint)
        self.request_card = QtWidgets.QWidget()
        request_layout = QtWidgets.QVBoxLayout(self.request_card)
        request_layout.setContentsMargins(0, 4, 0, 0)
        self.request_text = self.label('', 12)
        self.request_text.setWordWrap(True)
        request_layout.addWidget(self.request_text)
        actions = QtWidgets.QHBoxLayout()
        self.decline = QtWidgets.QPushButton('拒绝')
        self.accept = QtWidgets.QPushButton('接收')
        self.decline.clicked.connect(lambda: self.respond(False))
        self.accept.clicked.connect(lambda: self.respond(True))
        actions.addWidget(self.decline); actions.addWidget(self.accept)
        request_layout.addLayout(actions)
        layout.addWidget(self.request_card)
        self.progress = QtWidgets.QProgressBar()
        self.progress.setRange(0, 100); self.progress.setFixedHeight(7); self.progress.setTextVisible(False)
        layout.addWidget(self.progress)
        self.open_folder = QtWidgets.QPushButton('打开接收文件夹')
        self.open_folder.setMinimumHeight(32)
        self.open_folder.clicked.connect(self.folder)
        layout.addWidget(self.open_folder)
        self.request_card.hide(); self.progress.hide()
        service.changed.connect(self.render)
        service.request_received.connect(self.present)
        theme.changed.connect(self.restyle)
        self.restyle()
        self.render(service.state)

    @staticmethod
    def label(text, size, weight=W.Normal, center=False):
        label = QtWidgets.QLabel(text)
        label.setTextFormat(Qt.TextFormat.PlainText)
        label.setFont(ui_font(size, weight))
        if center:
            label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        return label

    def restyle(self):
        fg = theme.text().name()
        card = '#343641' if theme.dark else '#e9edf4'
        self.setStyleSheet(f'QLabel{{color:{fg};background:transparent}}'
                           f'QPushButton{{color:{fg};background:{card};border:0;border-radius:15px;padding:7px 10px;font-size:13px}}'
                           'QPushButton:checked{background:#367af5;color:white}'
                           'QPushButton:hover{border:1px solid #638fea}'
                           'QPushButton:disabled{color:#888}'
                           f'QProgressBar{{background:{card};border:0;border-radius:3px}}'
                           'QProgressBar::chunk{background:#4386ff;border-radius:3px}')
        self.update()

    def render(self, state):
        on, starting = state.get('isRunning', False), state.get('isStarting', False)
        self.name.setText(state.get('deviceName', 'AirDrop'))
        message = state.get('message', '点击重试连接')
        remaining = state.get('remainingSeconds')
        if remaining is not None and (on or starting):
            message += f'\n剩余 {remaining // 60:02d}:{remaining % 60:02d}'
        self.status.setText(message)
        self.off.setChecked(not on and not starting)
        self.everyone.setChecked(on or starting)
        self.everyone.setText('正在准备接收…' if starting else '所有人，10 分钟')
        self.everyone.setEnabled(state.get('ok', False) and not starting)
        self.off.setEnabled(state.get('ok', False))
        requests = state.get('requests') or []
        self._request_id = requests[0]['id'] if requests else None
        self.request_card.setVisible(bool(requests))
        if requests:
            request = requests[0]
            self.request_text.setText('来自 ' + request.get('sender', '附近的设备') + '\n' + '\n'.join(request.get('files', [])[:6]))
        self.accept.setEnabled(bool(requests)); self.decline.setEnabled(bool(requests))
        transfer = state.get('transfer') or {}
        phase = transfer.get('phase')
        self.progress.setVisible(phase == 'receiving')
        if phase == 'receiving':
            total = transfer.get('total', 0)
            self.progress.setRange(0, 100 if total else 0)
            self.progress.setValue(min(99, int(transfer.get('bytes', 0) * 100 / max(1, total))))
        if phase == 'received':
            self.status.setText('接收完成\n' + transfer.get('lastFile', '文件已保存'))
        self.open_folder.setEnabled(bool(state.get('receiveDirectory')))
        self.adjustSize()
        if self.isVisible():
            self._clamp()

    def present(self):
        self.render(self.service.state)
        self.show(); self.raise_(); self.activateWindow()
        if self.page.island:
            self.page.island.locks.add('airdrop')
            origin = self.page.mapToGlobal(QtCore.QPoint(self.page.width() + 16, 0))
        else:
            origin = QtGui.QCursor.pos()
        self.move(origin); self._clamp()

    def _clamp(self):
        screen = QtGui.QGuiApplication.screenAt(self.pos()) or QtGui.QGuiApplication.primaryScreen()
        if screen:
            area = screen.availableGeometry()
            self.move(max(area.left() + 8, min(self.x(), area.right() - self.width() - 8)),
                      max(area.top() + 8, min(self.y(), area.bottom() - self.height() - 8)))

    def respond(self, accept):
        if self._request_id:
            self.accept.setEnabled(False); self.decline.setEnabled(False)
            self.service.command('respond', id=self._request_id, accept=accept)

    def folder(self):
        directory = self.service.state.get('receiveDirectory')
        if directory:
            QtGui.QDesktopServices.openUrl(QtCore.QUrl.fromLocalFile(directory))

    def closeEvent(self, event):
        if self.page.island:
            self.page.island.locks.discard('airdrop')
        super().closeEvent(event)

    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton and event.position().y() < 60:
            self._drag_offset = event.globalPosition().toPoint() - self.frameGeometry().topLeft()

    def mouseMoveEvent(self, event):
        if self._drag_offset is not None and event.buttons() & Qt.MouseButton.LeftButton:
            self.move(event.globalPosition().toPoint() - self._drag_offset)

    def mouseReleaseEvent(self, event):
        self._drag_offset = None

    def paintEvent(self, event):
        p = QtGui.QPainter(self)
        p.setRenderHint(QtGui.QPainter.RenderHint.Antialiasing)
        rect = QtCore.QRectF(self.rect()).adjusted(1, 1, -1, -1)
        fill = QtGui.QLinearGradient(rect.topLeft(), rect.bottomRight())
        fill.setColorAt(0, QtGui.QColor(37, 40, 50, 248) if theme.dark else QtGui.QColor(255, 255, 255, 248))
        fill.setColorAt(1, QtGui.QColor(24, 27, 34, 244) if theme.dark else QtGui.QColor(240, 245, 253, 244))
        p.setBrush(fill); p.setPen(QtGui.QPen(QtGui.QColor(255, 255, 255, 65), 1))
        p.drawRoundedRect(rect, 30, 30)
