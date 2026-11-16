"""Original vector connectivity symbols and glass surfaces, shared by tile/detail."""
from PySide6 import QtCore, QtGui
from .theme import theme, ui_font, W
from .widgets import Hoverable, painter

Qt = QtCore.Qt
BLUE = QtGui.QColor('#0a9fff')


def draw_symbol(p, kind, rect, color=QtGui.QColor('white')):
    p.save()
    p.translate(rect.center()); p.scale(rect.width() / 24, rect.height() / 24)
    p.setBrush(Qt.BrushStyle.NoBrush)
    p.setPen(QtGui.QPen(color, 1.65, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap, Qt.PenJoinStyle.RoundJoin))
    if kind == 'airdrop':
        for radius in (10, 7, 4):
            p.drawArc(QtCore.QRectF(-radius, -radius, radius * 2, radius * 2), 305 * 16, 290 * 16)
        p.setPen(Qt.PenStyle.NoPen); p.setBrush(color)
        p.drawEllipse(QtCore.QPointF(0, 0), 1.5, 1.5)
    elif kind == 'wifi':
        for y, span in ((-6, 19), (-1.6, 12.4), (2.5, 5.8)):
            path = QtGui.QPainterPath(QtCore.QPointF(-span / 2, y))
            path.quadTo(0, y - span * .48, span / 2, y)
            p.drawPath(path)
        p.setBrush(color); p.setPen(Qt.PenStyle.NoPen)
        p.drawEllipse(QtCore.QPointF(0, 7), 1.7, 1.7)
    elif kind == 'bt':
        path = QtGui.QPainterPath(QtCore.QPointF(0, -10)); path.lineTo(0, 10)
        path.moveTo(-6, -5); path.lineTo(6, 5); path.lineTo(0, 10)
        path.moveTo(-6, 5); path.lineTo(6, -5); path.lineTo(0, -10)
        p.drawPath(path)
    p.restore()


def glass_surface(p, widget, rect, radius, island=None, strength=1.0):
    path = QtGui.QPainterPath(); path.addRoundedRect(rect, radius, radius)
    backdrop = getattr(island, 'backdrop', None)
    if backdrop is not None:
        brush = backdrop.brush_for(backdrop.state, widget.mapToGlobal(QtCore.QPoint()))
        if brush is not None:
            p.fillPath(path, brush)
    tint = theme.tint()
    tint.setAlpha(min(235, int(tint.alpha() * strength)))
    p.fillPath(path, tint)
    gloss = QtGui.QLinearGradient(rect.topLeft(), rect.bottomRight())
    gloss.setColorAt(0, QtGui.QColor(255, 255, 255, 42 if theme.dark else 115))
    gloss.setColorAt(.45, QtGui.QColor(255, 255, 255, 5))
    gloss.setColorAt(1, QtGui.QColor(255, 255, 255, 17))
    p.fillPath(path, gloss)
    rim = QtGui.QLinearGradient(rect.topLeft(), rect.bottomLeft())
    rim.setColorAt(0, QtGui.QColor(255, 255, 255, 175 if theme.dark else 235))
    rim.setColorAt(.5, QtGui.QColor(255, 255, 255, 28))
    rim.setColorAt(1, QtGui.QColor(255, 255, 255, 90))
    p.strokePath(path, QtGui.QPen(QtGui.QColor(0, 0, 0, 45), 2))
    p.strokePath(path, QtGui.QPen(QtGui.QBrush(rim), 1))


class ConnectionTile(Hoverable):
    def __init__(self, kind, title, compact=False):
        super().__init__()
        self.kind, self.title, self.compact = kind, title, compact
        self.on, self.sub, self.enabled_ = False, '', True
        self.island = None
        self.setFixedHeight(62 if compact else 118)
        self.setMinimumWidth(120)
        self.setAccessibleName(title)

    def set_state(self, on, sub='', enabled=True):
        self.on, self.sub, self.enabled_ = bool(on), sub, enabled
        self.setAccessibleDescription(sub)
        self.update()

    def paintEvent(self, event):
        p = painter(self)
        rect = QtCore.QRectF(self.rect()).adjusted(1.5, 1.5, -1.5, -1.5)
        scale = self.press_scale()
        p.translate(rect.center()); p.scale(scale, scale); p.translate(-rect.center())
        glass_surface(p, self, rect, 27 if not self.compact else 30, self.island)
        circle = QtCore.QRectF(14, 12 if not self.compact else 11, 40, 40)
        p.setPen(Qt.PenStyle.NoPen)
        off_circle = QtGui.QColor(255, 255, 255, 48) if theme.dark else QtGui.QColor(105, 112, 124, 175)
        p.setBrush(BLUE if self.on else off_circle)
        p.drawEllipse(circle)
        draw_symbol(p, self.kind, circle.adjusted(8, 8, -8, -8))
        x, y = (65, 10) if self.compact else (16, 69)
        width = self.width() - x - 13
        p.setFont(ui_font(13, W.DemiBold)); p.setPen(theme.text())
        p.drawText(QtCore.QRectF(x, y, width, 20), Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter, self.title)
        p.setFont(ui_font(10.5)); p.setPen(theme.text2())
        fm = QtGui.QFontMetrics(p.font())
        text = fm.elidedText(self.sub, Qt.TextElideMode.ElideRight, max(1, width - (16 if self.kind == 'airdrop' else 0)))
        p.drawText(QtCore.QRectF(x, y + 20, width, 17), Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter, text)
        if self.kind == 'airdrop':
            p.setPen(QtGui.QPen(theme.text2(), 1.2, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
            cx, cy = rect.right() - 15, y + 29
            p.drawLine(QtCore.QPointF(cx - 2.5, cy - 2), QtCore.QPointF(cx, cy - 4.5))
            p.drawLine(QtCore.QPointF(cx, cy - 4.5), QtCore.QPointF(cx + 2.5, cy - 2))
            p.drawLine(QtCore.QPointF(cx - 2.5, cy + 2), QtCore.QPointF(cx, cy + 4.5))
            p.drawLine(QtCore.QPointF(cx, cy + 4.5), QtCore.QPointF(cx + 2.5, cy + 2))
