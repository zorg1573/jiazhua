using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace jiazhua
{
    public partial class DoubleBufferedPanelCloud32 : Panel
    {
        private Bitmap backgroundCache;
        private Bitmap heatmapCache;
        private bool cacheInvalid = true;
        private float fontHeight;
        private double[] values = new double[32]; // 32通道数据
        private double[] valuesTemp = new double[32]; // 32通道数据
        private Rectangle[] dotRects;
        private double maxValueColor = 800;

        // 新增属性：由高斯拟合结果提供
        public double CenterX { get; set; } = 100;
        public double CenterY { get; set; } = 100;
        public double Sigma { get; set; } = 0;     // 扩散半径
        public double Amplitude { get; set; } = 0; // 最大值
        public bool Guiyihua { get; set; } = false;

        public DoubleBufferedPanelCloud32()
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

            // 我们需要一个临时数组来存储按布局顺序生成的矩形
            Rectangle[] tempDotRects = new Rectangle[values.Length];

            // 定义布局：6 列，每列的行数
            int[] colRows = { 1, 7, 6, 6, 6, 6 };
            int numCols = colRows.Length; // 列数 = 6

            // 定义传感器通道到屏幕布局索引的映射关系 (1-based channel -> 0-based value index)
            // 屏幕上点的布局顺序 (tempDotRects[i]) 应该对应哪个通道的数据 (values[channel - 1])
            // 传感器真实顺序：[1, 14, 13, 6, 5, 4, 3, 2, 20, 19, 18, 17, 16, 15, 10, 9, 8, 7, 22, 21, 26, 25, 24, 23, 12, 11, 32, 31, 30, 29, 28, 27]
            int[] channelOrder = new int[]
            {
                16,7,8,9,10,11,12,13,6,5,4,3,2,1,23,24,25,26,15,14,27,28,29,30,31,32,22,21,20,19,18,17
            };

            // 确保映射数组长度正确
            if (channelOrder.Length != values.Length)
            {
                dotRects = new Rectangle[values.Length]; // 失败时初始化空数组
                return;
            }

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                // ---------- 区域和间距计算 (保持不变) ----------
                int margin = 10;
                int drawWidth = this.Width - 2 * margin;
                int drawHeight = this.Height - 2 * margin;

                double standardSpacingRatio = 0.5;
                double largeSpacingRatio = 2;

                int maxRows = colRows.Max();

                double totalHeightFactor = maxRows * (1 + standardSpacingRatio);
                int maxHeightDiameter = (int)(drawHeight / totalHeightFactor);

                double totalWidthFactor = 2.0 * numCols - 1.0;
                int maxWidthDiameter = (int)(drawWidth / totalWidthFactor);

                int circleDiameter = Math.Min(maxHeightDiameter, maxWidthDiameter);

                int standardVerticalSpacing = (int)(circleDiameter * standardSpacingRatio);
                int largeVerticalSpacing = (int)(circleDiameter * largeSpacingRatio);
                int horizontalSpacing = circleDiameter;

                int totalDotWidth = numCols * circleDiameter + (numCols - 1) * horizontalSpacing;
                int startX = margin + (drawWidth - totalDotWidth) / 2;

                int layoutIndex = 0; // 用于填充 tempDotRects 的索引
                int currentX = startX;

                // ---------- 循环生成 6 列传感器点矩形 (按布局顺序填充 tempDotRects) ----------
                for (int col = 0; col < numCols; col++)
                {
                    int rowsInCol = colRows[col];

                    int currentVerticalSpacing;
                    if (col >= 2)
                    {
                        currentVerticalSpacing = largeVerticalSpacing;
                    }
                    else
                    {
                        currentVerticalSpacing = standardVerticalSpacing;
                    }

                    int colHeight = rowsInCol * circleDiameter + (rowsInCol > 0 ? (rowsInCol - 1) * currentVerticalSpacing : 0);
                    int startY = margin + (drawHeight - colHeight) / 2;

                    int currentY = startY;

                    for (int row = 0; row < rowsInCol; row++)
                    {
                        if (layoutIndex >= tempDotRects.Length) break;

                        // 1. 在这里，我们仅仅计算屏幕上的矩形位置
                        tempDotRects[layoutIndex] = new Rectangle(currentX, currentY, circleDiameter, circleDiameter);

                        currentY += (circleDiameter + currentVerticalSpacing);
                        layoutIndex++;
                    }

                    currentX += (circleDiameter + horizontalSpacing);
                }
            }

            // ---------- 7. 最终映射和修正 dotRects 数组 ----------
            // 创建最终的 dotRects 数组，其索引 i 对应 values[i] 的数据，其值是屏幕上的正确位置。
            dotRects = new Rectangle[values.Length];

            // 遍历所有通道 (values 的索引 i)
            for (int i = 0; i < values.Length; i++)
            {
                // 找到第 i 个传感器通道 (i+1) 在屏幕布局中的位置索引 (layoutIndex)
                // 逻辑：
                // 1. 找到 i+1 对应在 channelOrder 中的位置 k (k = layoutIndex)
                // 2. 将 tempDotRects[k] 赋值给 dotRects[i]

                // 找到值为 i+1 的元素在 channelOrder 中的索引 k
                int layoutIndex = Array.IndexOf(channelOrder, i + 1);

                if (layoutIndex != -1 && layoutIndex < tempDotRects.Length)
                {
                    // 将按顺序生成的屏幕位置 (tempDotRects[layoutIndex]) 
                    // 赋给按数据顺序排列的 dotRects[i]
                    dotRects[i] = tempDotRects[layoutIndex];
                }
            }
        }
        /*        private void GenerateBackground()
                {
                    backgroundCache?.Dispose();
                    backgroundCache = new Bitmap(this.Width, this.Height);
                    dotRects = new Rectangle[values.Length];

                    // 定义布局：6 列，每列的行数
                    int[] colRows = { 1, 7, 6, 6, 6, 6 };
                    int numCols = colRows.Length; // 列数 = 6

                    using (var g = Graphics.FromImage(backgroundCache))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(this.BackColor);

                        // ---------- 区域和间距计算 ----------
                        int margin = 10;
                        int drawWidth = this.Width - 2 * margin;
                        int drawHeight = this.Height - 2 * margin;

                        // 1. 定义间距比例
                        double standardSpacingRatio = 0.5; // 50%
                        double largeSpacingRatio = 2;    // 150% (增大后的间距)

                        // 确定最大行数 MaxRows = 7
                        int maxRows = colRows.Max();

                        // 2. 计算【垂直约束下的最大点径】(MaxHeightDiameter)
                        // 假设最大行数 7 都使用标准间距 (1 + 0.5 = 1.5 倍直径)
                        double totalHeightFactor = maxRows * (1 + standardSpacingRatio);
                        // 如果垂直总高度超过了 drawHeight，需要缩小点径
                        int maxHeightDiameter = (int)(drawHeight / totalHeightFactor);


                        // 3. 计算【水平约束下的最大点径】(MaxWidthDiameter)
                        // 横向间距 (horizontalSpacing) 保持为点径大小 (1倍直径)
                        // 总宽度因子 = (numCols * Diameter) + (numCols - 1) * (1 * Diameter)
                        // 总宽度因子 = numCols + numCols - 1 = 2 * numCols - 1
                        double totalWidthFactor = 2.0 * numCols - 1.0;
                        int maxWidthDiameter = (int)(drawWidth / totalWidthFactor);


                        // 4. 确定最终点径：取垂直和水平约束下的最小值
                        int circleDiameter = Math.Min(maxHeightDiameter, maxWidthDiameter);

                        // 5. 重新计算实际使用的间距
                        int standardVerticalSpacing = (int)(circleDiameter * standardSpacingRatio);
                        int largeVerticalSpacing = (int)(circleDiameter * largeSpacingRatio);
                        int horizontalSpacing = circleDiameter;

                        // 6. 重新计算总宽度和起始 X（使用新的 circleDiameter）
                        int totalDotWidth = numCols * circleDiameter + (numCols - 1) * horizontalSpacing;
                        int startX = margin + (drawWidth - totalDotWidth) / 2; // 水平居中起点

                        int valueIndex = 0;
                        int currentX = startX;

                        // ---------- 循环绘制 6 列传感器点 (按列居中) ----------
                        for (int col = 0; col < numCols; col++)
                        {
                            int rowsInCol = colRows[col];

                            // 确定当前列使用的纵向间距
                            int currentVerticalSpacing;
                            if (col >= 2) // 索引 2, 3, 4, 5 对应第 3, 4, 5, 6 列
                            {
                                currentVerticalSpacing = largeVerticalSpacing;
                            }
                            else // 索引 0, 1 对应第 1, 2 列
                            {
                                currentVerticalSpacing = standardVerticalSpacing;
                            }

                            // 重新计算当前列的总高度 
                            int colHeight = rowsInCol * circleDiameter + (rowsInCol > 0 ? (rowsInCol - 1) * currentVerticalSpacing : 0);

                            // 计算当前列的垂直居中起点 Y 坐标 
                            int startY = margin + (drawHeight - colHeight) / 2;

                            int currentY = startY;

                            for (int row = 0; row < rowsInCol; row++)
                            {
                                if (valueIndex >= values.Length) break;

                                // 计算当前点的矩形位置
                                dotRects[valueIndex] = new Rectangle(currentX, currentY, circleDiameter, circleDiameter);

                                // 使用当前列的间距进行 Y 坐标累加
                                currentY += (circleDiameter + currentVerticalSpacing);
                                valueIndex++;
                            }

                            // 移动到下一列的起始 X 坐标 
                            currentX += (circleDiameter + horizontalSpacing);
                        }
                    }
        *//*            if (dotRects.Length == 32)
                    {
                        var tmp = dotRects[1];
                        dotRects[1] = dotRects[13];
                        dotRects[13] = tmp;

                        tmp = dotRects[2];
                        dotRects[2] = dotRects[12];
                        dotRects[12] = tmp;

                        tmp = dotRects[3];
                        dotRects[3] = dotRects[5];
                        dotRects[5] = tmp;

                        tmp = dotRects[6];
                        dotRects[6] = dotRects[2];
                        dotRects[2] = tmp;

                        tmp = dotRects[7];
                        dotRects[7] = dotRects[1];
                        dotRects[1] = tmp;

                        tmp = dotRects[6];
                        dotRects[6] = dotRects[2];
                        dotRects[2] = tmp;
                    }*//*
                }*/

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止默认背景清除
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

    }
}