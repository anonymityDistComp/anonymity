using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Anonymity
{
    public partial class Form1 : Form
    {
        private List<List<DrawData>> d;
        private int selectedI = -1;
        private int selectedJ = -1;
        private int hoverI = -1;
        private int hoverJ = -1;
        private Graphics g;
        private bool quality = false;
        private bool drawLinks = false;
        private bool writeMultiplicities = false;
        private bool carefulAlgoritm = true;
        private bool lastReset = false;
        private float radius = 25;
        private int count = -1;
        private int mouseX, mouseY;

        private SolidBrush circleBrush = new SolidBrush(Color.White);
        private SolidBrush panelBrush = new SolidBrush(Color.LightGray);

        private Pen blackPen = new Pen(Color.Black, 3);
        private Pen redPen = new Pen(Color.Red, 1);

        private static String fontName = "Verdana";
        Font font = new Font(fontName, 40, FontStyle.Regular, GraphicsUnit.Pixel);
        Font panelFont = new Font(fontName, 25, FontStyle.Regular, GraphicsUnit.Pixel);
        Brush fontBrush = new SolidBrush(Color.Black);
        StringFormat stringFormat = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        StringFormat panelFormat = new StringFormat()
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };

        private class DrawData
        {
            public HistoryTree h;
            public int parent = -1; // index of parent in previous level
            public List<int> children = new List<int>(); // indices of children in next level
            public List<int> observations = new List<int>(); // indices of nodes in previous level
            public List<int> observed = new List<int>(); // indices of nodes in next level
            public int width;
            public float x, y, dx, dy;
        }

        private int DrawDataHelper1(HistoryTree h, List<List<DrawData>> d)
        {
            int index = h.level + 1;
            while (d.Count <= index) d.Add(new List<DrawData>());
            var data = new DrawData();
            d[index].Add(data);
            data.h = h;
            if (index > 0) data.parent = d[index - 1].Count - 1;
            if (h.children.Count > 0)
            {
                foreach (var c in h.children)
                {
                    data.width += DrawDataHelper1(c, d);
                    data.children.Add(d[index + 1].Count - 1);
                }
            }
            if (data.width == 0) data.width = 1;
            return data.width;
        }

        private void DrawDataHelper2(List<List<DrawData>> d, int i = 0, int j = 0, float x1 = 0, float y1 = 0, float x2 = 1, float y2 = 1)
        {
            var data = d[i][j];
            int w = data.width;
            int h = d.Count;
            data.y = y1 + (y2 - y1) * (i + 0.5f) / h;
            if (data.children.Count == 0) data.x = (x1 + x2) / 2;
            else
            {
                float nx1 = x1;
                foreach (int k in data.children)
                {
                    float nx2 = nx1 + (x2 - x1) * d[i + 1][k].width / w;
                    DrawDataHelper2(d, i + 1, k, nx1, y1, nx2, y2);
                    nx1 = nx2;
                }
                data.x = (d[i + 1][data.children[0]].x + d[i + 1][data.children[data.children.Count - 1]].x) / 2;
            }
            foreach (var a in data.h.observations)
                for (int k = 0; k < d[i - 1].Count; k++)
                    if (a.Item1 == d[i - 1][k].h) { data.observations.Add(k); break; }
            foreach (var a in data.h.observed)
                for (int k = 0; k < d[i + 1].Count; k++)
                    if (a.Item1 == d[i + 1][k].h) { data.observed.Add(k); break; }
        }

        private List<List<DrawData>> ComputeDrawData(HistoryTree h)
        {
            var d = new List<List<DrawData>>();
            DrawDataHelper1(h, d);
            DrawDataHelper2(d);
            return d;
        }

        public Form1()
        {
            HistoryTest.Test();
            d = ComputeDrawData(HistoryTest.h);
            InitializeComponent();
        }

        private void DrawPanel(DrawData data)
        {
            int i = 0, delta = 30;
            g.FillRectangle(panelBrush, data.dx, data.dy, 200, delta * 5);
            g.DrawString("Level: " + data.h.level, panelFont, fontBrush, data.dx, data.dy + delta * i++, panelFormat);
            g.DrawString("Weight: " + data.h.weight, panelFont, fontBrush, data.dx, data.dy + delta * i++, panelFormat);
            g.DrawString("CumAnon: " + data.h.cumulativeAnonymity, panelFont, fontBrush, data.dx, data.dy + delta * i++, panelFormat);
            g.DrawString("Guesser: " + data.h.guesser, panelFont, fontBrush, data.dx, data.dy + delta * i++, panelFormat);
            g.DrawString("Cut: " + data.h.cut, panelFont, fontBrush, data.dx, data.dy + delta * i++, panelFormat);
        }

        private void DrawNode(DrawData data, bool selected = false)
        {
            int guess = data.h.guess;
            if (!data.h.selected) circleBrush.Color = Color.White;
            else if (data.h.level == -1) circleBrush.Color = count == -1 ? Color.OrangeRed : Color.LightBlue;
            else if (data.h.correct) circleBrush.Color = Color.LightGreen;
            else circleBrush.Color = guess != -1 ? Color.Orange : Color.Yellow;
            g.FillEllipse(circleBrush, data.dx - radius, data.dy - radius, 2 * radius, 2 * radius);
            blackPen.Color = data.h.selected ? Color.Black : Color.Gray;
            g.DrawEllipse(blackPen, data.dx - radius, data.dy - radius, 2 * radius, 2 * radius);
            float innerRadius = radius * 0.8f;
            if (data.h.input == 0) g.DrawEllipse(blackPen, data.dx - innerRadius, data.dy - innerRadius, 2 * innerRadius, 2 * innerRadius);
            if (guess != -1) g.DrawString(guess == -1 ? "?" : guess.ToString(), font, fontBrush, data.dx, data.dy, stringFormat);
            //g.DrawString(data.h.cumulativeAnonymity.ToString(), font, fontBrush, data.dx, data.dy, stringFormat);
            //g.DrawString(data.h.weight.ToString(), font, fontBrush, data.dx, data.dy, stringFormat);
            if (data.h.level == -1 && count != -1) g.DrawString(count == -1 ? "?" : count.ToString(), font, fontBrush, data.dx, data.dy, stringFormat);
        }

        private void DrawHistoryTree(List<List<DrawData>> d, float x1, float y1, float x2, float y2)
        {
            for (int i = 0; i < d.Count; i++)
                for (int j = 0; j < d[i].Count; j++)
                {
                    var data = d[i][j];
                    data.dx = x1 + data.x * (x2 - x1);
                    data.dy = y1 + data.y * (y2 - y1);
                    if (data.parent != -1)
                    {
                        var parent = d[i - 1][data.parent];
                        if (data.h.selected)
                        {
                            blackPen.Color = Color.Black;
                            blackPen.Width = 8;
                        }
                        else
                        {
                            blackPen.Color = Color.Gray;
                            blackPen.Width = 3;
                        }
                        g.DrawLine(blackPen, data.dx, data.dy, parent.dx, parent.dy);
                    }
                }
            if (drawLinks)
                for (int i = 0; i < d.Count; i++)
                    for (int j = 0; j < d[i].Count; j++)
                    {
                        var data = d[i][j];
                        data.dx = x1 + data.x * (x2 - x1);
                        data.dy = y1 + data.y * (y2 - y1);
                        //if (!data.selected) continue; // REMOVE!!!
                        redPen.Width = data.h.selected ? 4 : 1;
                        foreach (int k in data.observations)
                        {
                            //if (k != 0) continue; // REMOVE!!!

                            var obs = d[i - 1][k];
                            g.DrawLine(redPen, data.dx, data.dy, obs.dx, obs.dy);
                            if (writeMultiplicities)
                            {
                                foreach (var x in data.h.observations)
                                    if (x.Item1 == obs.h)
                                    {
                                        g.DrawString(x.Item2.ToString(), font, fontBrush, (3 * data.dx + obs.dx) / 4, (3 * data.dy + obs.dy) / 4, stringFormat);
                                        break;
                                    }
                            }
                        }
                    }
            if (d[0][0].h.guess != -1)
            {
                float ly = d[d[0][0].h.deepest + 1][0].dy;
                blackPen.Color = Color.Blue;
                blackPen.Width = 5;
                g.DrawLine(blackPen, x1, ly, x2, ly);
            }
            blackPen.Width = 3;
            for (int i = 0; i < d.Count; i++)
                for (int j = 0; j < d[i].Count; j++)
                    DrawNode(d[i][j], i == selectedI && j == selectedJ);
            if (hoverI != -1 && hoverJ != -1) DrawPanel(d[hoverI][hoverJ]);
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            g = e.Graphics;
            if (quality)
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
            }
            else
            {
                g.SmoothingMode = SmoothingMode.HighSpeed;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.InterpolationMode = InterpolationMode.Low;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            }
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            float newR = 25;
            float m = w / (2 * d[0][0].width);
            if (newR > m) newR = m;
            m = h / (2 * d.Count);
            if (newR > m) newR = m;
            if (newR != radius)
            {
                radius = newR;
                float emSize = radius * 8 / 5;
                if (emSize < 1) emSize = 1;
                font = new Font(fontName, emSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            DrawHistoryTree(d, 0, 0, w, h);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            Invalidate(false);
        }

        private bool ResetSelection(bool allSelected)
        {
            if (selectedI == -1 && selectedJ != -1) return false;
            d[0][0].h.ResetCounts(true);
            selectedI = selectedJ = -1;
            for (int i = 0; i < d.Count; i++)
                for (int j = 0; j < d[i].Count; j++)
                    d[i][j].h.selected = allSelected;
            return true;
        }

        private void SelectNode(int i, int j)
        {
            if (i < 0) return;
            var data = d[i][j];
            if (data.h.selected) return;
            data.h.selected = true;
            if (data.parent != -1) SelectNode(i - 1, data.parent);
            foreach (int k in data.observations) SelectNode(i - 1, k);
        }

        (int, int) GetNodeXY(int x, int y, float r)
        {
            int si = -1, sj = -1;
            float minDist = -1;
            for (int i = 0; i < d.Count; i++)
            {
                float ddy = d[i][0].dy;
                if (ddy + r < y) continue;
                if (ddy - r > y) break;
                for (int j = 0; j < d[i].Count; j++)
                {
                    var data = d[i][j];
                    float dx = data.dx - x;
                    float dy = data.dy - y;
                    float dist = dx * dx + dy * dy;
                    if (si == -1 || dist < minDist)
                    {
                        minDist = dist;
                        si = i;
                        sj = j;
                    }
                }
            }
            if (minDist <= r * r) return (si, sj);
            else return (-1, -1);
        }

        private void CountAgents(bool reset, int si = -1, int sj = -1)
        {
            lastReset = reset;
            ResetSelection(reset);
            if (reset) count = d[0][0].h.CountAgents(false, carefulAlgoritm);
            else
            {
                selectedI = si;
                selectedJ = sj;
                if (si != -1 && sj != -1)
                {
                    SelectNode(si, sj);
                    count = d[0][0].h.CountAgents(false, carefulAlgoritm);
                }
                else count = -1;
            }
            Invalidate(false);
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                (int si, int sj) = GetNodeXY(e.X, e.Y, radius);
                if (si != selectedI || sj != selectedJ) CountAgents(false, si, sj);
            }
            if ((e.Button & MouseButtons.Right) == MouseButtons.Right) CountAgents(true);
        }

        private void NoHover()
        {
            if (hoverI == -1 && hoverJ == -1) return;
            hoverI = hoverJ = -1;
            Invalidate(false);
        }

        private void Hover(int x, int y)
        {
            (int si, int sj) = GetNodeXY(x, y, 2 * radius);
            if (si != hoverI || sj != hoverJ)
            {
                hoverI = si;
                hoverJ = sj;
                Invalidate(false);
            }
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            mouseX = e.X; mouseY = e.Y;
            if (ModifierKeys.HasFlag(Keys.Control)) Hover(mouseX, mouseY);
            else NoHover();
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.ControlKey: NoHover(); break;
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.ControlKey: Hover(mouseX, mouseY); break;
                case Keys.Q: quality = !quality; Invalidate(false);  break;
                case Keys.L: drawLinks = !drawLinks; Invalidate(false); break;
                case Keys.M: writeMultiplicities = !writeMultiplicities; Invalidate(false); break;
                case Keys.A: carefulAlgoritm = !carefulAlgoritm; CountAgents(lastReset, selectedI, selectedJ); break;
            }
        }
    }
}