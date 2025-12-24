using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace jiazhua
{
    public partial class DoubleBufferedPanelCloud12 : Panel
    {
        private Bitmap backgroundCache;
        private Bitmap heatmapCache;
        private bool cacheInvalid = true;
        private float fontHeight;
        private double[] values = new double[12]; // 12通道数据
        private double[] valuesTemp = new double[12]; // 12通道数据
        private Rectangle[] dotRects;
        private double maxValueColor = 800;

        // 新增属性：由高斯拟合结果提供
        public double CenterX { get; set; } = 100;
        public double CenterY { get; set; } = 100;
        public double Sigma { get; set; } = 0;     // 扩散半径
        public double Amplitude { get; set; } = 0; // 最大值
        public bool Guiyihua { get; set; } = false;

        public DoubleBufferedPanelCloud12()
        {
            this.DoubleBuffered = true;
            this.Resize += (_, __) => { cacheInvalid = true; };
            this.FontChanged += (_, __) => CacheFontHeight();
            CacheFontHeight();
        }

        private void CacheFontHeight()
        {
            using (Graphics g = CreateGraphics())
                fontHeight = g.MeasureString("0", this.Font).Height;
        }
        public double[] Values
        {
            get => values; set
            {
                if (value != null && value.Length == values.Length)
                {
                    Array.Copy(value, values, values.Length);
                }
            }
        }
        public double[] ValuesTemp
        {
            get => valuesTemp; set
            {
                if (value != null && value.Length == valuesTemp.Length)
                {
                    Array.Copy(value, valuesTemp, valuesTemp.Length);
                }
            }
        }
        public Rectangle[] DotRectss
        {
            get => dotRects;
        }
        public double MaxValueColor
        {
            get => maxValueColor; set
            {
                if (value > 0)
                    maxValueColor = value;
            }
        }

        private void GenerateBackground()
        {
            backgroundCache?.Dispose();
            backgroundCache = new Bitmap(this.Width, this.Height);
            dotRects = new Rectangle[values.Length];

            // 定义布局：5 行，列数分别为 3, 3, 2, 2, 2
            int[] rowCols = { 3, 3, 2, 2, 2 };
            int numRows = rowCols.Length; // 行数 = 5

            // 定义宽度比例
            const double standardWidthRatio = 0.8; // 1, 2 行使用 80% 宽度
            const double narrowWidthRatio = 0.6;   // 3, 4, 5 行使用 60% 宽度 (间距缩小)

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                // ---------- 区域和间距计算 ----------
                int marginX = 0;
                int marginY = 0;
                int rectWidth = this.Width - 2 * marginX;
                int rectHeight = this.Height - 2 * marginY;

                // 计算圆点直径和垂直间距 (保持不变)
                int circleDiameter = Math.Min(this.Width, this.Height) / 10;
                int totalDotHeight = numRows * circleDiameter;
                int remainingHeight = rectHeight - totalDotHeight;
                int verticalSpacing = (numRows > 1) ? remainingHeight / (numRows - 1) : 0;

                int valueIndex = 0; // 对应 values 数组的索引

                // ---------- 循环绘制 5 行传感器点 ----------
                for (int row = 0; row < numRows; row++)
                {
                    int cols = rowCols[row];

                    // 1. 根据行索引确定宽度比例
                    double currentWidthRatio;
                    if (row >= 2) // 索引 2, 3, 4 对应第 3, 4, 5 行
                    {
                        currentWidthRatio = narrowWidthRatio;
                    }
                    else // 索引 0, 1 对应第 1, 2 行
                    {
                        currentWidthRatio = standardWidthRatio;
                    }

                    // 计算当前行起点 Y 坐标
                    int y = marginY + row * circleDiameter + row * verticalSpacing;

                    // --- 每行的横向分布（矩形等间距） ---
                    // 2. 使用确定的宽度比例计算行宽
                    double rowWidth = rectWidth * currentWidthRatio;

                    // 计算起始 X 坐标 (确保居中)
                    double startX = marginX + (rectWidth - rowWidth) / 2.0;

                    // 3. 重新计算行内点间距 (此时 rowWidth 较小，spacing 会相应变小)
                    double spacing = (cols > 1) ? (rowWidth - circleDiameter * cols) / (cols - 1) : 0;

                    // 确保 spacing >= 0
                    spacing = Math.Max(0, spacing);


                    for (int col = 0; col < cols; col++)
                    {
                        if (valueIndex >= values.Length) break;

                        // 计算 X 坐标：起始 X + col * (直径 + 间距)
                        int x = (int)(startX + col * (circleDiameter + spacing));

                        dotRects[valueIndex] = new Rectangle(x, y, circleDiameter, circleDiameter);
                        valueIndex++;
                    }
                }

            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止默认背景清除
        }
        private Color GetColorFromValue(double value)
        {
            //double ratio = value / Amplitude;
            if (value > 0)
            {
                double ratio = value;
                if (ratio > 1) ratio = 1;
                if (ratio < 0) ratio = 0;
                int r = 0, g = 0, b = 0;
                if (ratio < 0.33)
                {
                    double t = ratio / 0.33;
                    r = 0;
                    g = (int)(255 * t);
                    b = (int)(255 * (1 - t));
                }
                else if (ratio < 0.66)
                {
                    double t = (ratio - 0.33) / 0.33;
                    r = (int)(255 * t);
                    g = 255;
                    b = 0;
                }
                else
                {
                    double t = (ratio - 0.66) / 0.34;
                    r = 255;
                    g = (int)(255 * (1 - t));
                    b = 0;
                }
                int a = (int)(255 * ratio);
                return Color.FromArgb(a, r, g, b);
            }
            else
            {
                return Color.Empty;
            }

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // ---------- 1️⃣ 绘制背景图（铺满） ----------
                if (this.BackgroundImage != null)
                {
                    // 拉伸铺满整个Panel
                    g.DrawImage(this.BackgroundImage, this.ClientRectangle);
                }
                else
                {
                    // 没设置背景图时使用背景色
                    g.Clear(this.BackColor);
                }

                // ---------- 2️⃣ 绘制梯形和传感器点的底图 ----------
                GenerateBackground();

                if (backgroundCache != null)
                    g.DrawImageUnscaled(backgroundCache, Point.Empty);

                // ---------- 绘制传感器值 ----------
                // ---------- 绘制传感器值 (压力 P 和 温度 T) ----------
                for (int i = 0; i < values.Length && i < dotRects.Length; i++)
                {
                    var rect = dotRects[i];

                    // 压力值 (P) 和 温度值 (T)
                    double pressureValue = values[i];
                    double temperatureValue = valuesTemp[i];

                    string pressureText = "P" + pressureValue.ToString("F0");
                    string temperatureText = "T" + temperatureValue.ToString("F0");

                    // 文本测量 (我们假设 P 和 T 文本的宽度相似，使用 P 文本测量高度)
                    SizeF tsP = g.MeasureString(pressureText, this.Font);

                    // 将圆点矩形的高度平分给两行文本
                    float lineYOffset = rect.Height / 2f;
                    float textYCenter = (lineYOffset - tsP.Height) / 2f; // 文本在半个圆点矩形内垂直居中


                    // 1. 绘制压力值 (P)
                    g.DrawString(
                        pressureText,
                        this.Font,
                        Brushes.White,
                        rect.X + (rect.Width - tsP.Width) / 2f, // X 轴居中
                        rect.Y + textYCenter                    // 位于上半部分居中
                    );

                    // 2. 绘制温度值 (T)
                    // T 文本的起始 Y 坐标 = rect.Y + 上半部分高度 + T 文本在上半部分中的 Y 居中偏移
                    g.DrawString(
                        temperatureText,
                        this.Font,
                        Brushes.White,
                        rect.X + (rect.Width - tsP.Width) / 2f, // X 轴居中
                        rect.Y + lineYOffset + textYCenter      // 位于下半部分居中
                    );
                }
                //DrawForceArrow8(g);
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnPaint Exception: " + ex.Message);
            }
        }

        private void DrawForceArrow8(Graphics g)
        {
            if (values == null || values.Length != 8) return;
            float cx = this.Width / 2f;
            float cy = this.Height / 2f;
            PointF[] dirs = new PointF[8];
            int index = 0;
            float rowSpacing = this.Height / 3f;
            //float colSpacingTop = this.Width / 3f;
            //float colSpacingMiddle = this.Width / 4f;
            // float colSpacingBottom = this.Width / 4f;
            float colSpacingTop = this.Width - 2;
            float colSpacingMiddle = this.Width - 2;
            float colSpacingBottom = this.Width - 2;
            dirs[index++] = new PointF(1, -1);  // 左
            dirs[index++] = new PointF(-1, -1); // 右
            dirs[index++] = new PointF(1, 0);   // 左
            dirs[index++] = new PointF(0, 0);   // 中
            dirs[index++] = new PointF(-1, 0);  // 右
            dirs[index++] = new PointF(1, 1);   // 左
            dirs[index++] = new PointF(0, 1);   // 中
            dirs[index++] = new PointF(-1, 1);  // 右
            float fx = 0, fy = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float mag = (float)values[i];
                fx += dirs[i].X * mag;
                fy += dirs[i].Y * mag;
            }
            float len = (float)Math.Sqrt(fx * fx + fy * fy);
            if (len < 1e-3) return;
            float scale = Math.Min(this.Width, this.Height) / 4f / len;
            fx *= scale;
            fy *= scale;
            float tx = cx + fx;
            float ty = cy + fy;
            using (Pen pen = new Pen(Color.White, 3))
            {
                pen.CustomEndCap = new AdjustableArrowCap(6, 8, true);
                g.DrawLine(pen, cx, cy, tx, ty);
            }
        }

    }
}