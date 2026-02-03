using DocumentFormat.OpenXml.Spreadsheet;
using ReaLTaiizor.Controls;
using ScottPlot;
using ScottPlot.WinForms;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Windows.Forms.Design;

namespace jiazhua
{
    public partial class MainForm : Form
    {
        private string savePath = "";
        private bool guiyihua = false;
        private int saveRate = 100;
        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            crownTextBox_addr12.Text = "1";
            crownTextBox_addr32.Text = "2";
            crownTextBox_sendRate.Text = "25";
            var data = new List<object>();

            for (int i = 1; i <= 12; i++)
            {
                data.Add(new { Value = i, Text = $"CH{i}" });
            }

            // 绑定到多选 ComboBox
            uCheckComboBox1.BindingDataList(data, "Value", "Text");
            // 默认全选
            uCheckComboBox1.CheckAll();

            var data2 = new List<object>();

            for (int i = 1; i <= 32; i++)
            {
                data2.Add(new { Value = i, Text = $"CH{i}" });
            }

            // 绑定到多选 ComboBox
            uCheckComboBox2.BindingDataList(data2, "Value", "Text");
            // 默认全选
            uCheckComboBox2.CheckAll();
            LoadPorts();
            LoadFromJson();
            StartPacketProcessingThread();
            InitializePlots();
            InitializePlotTimer();
        }

        private void LoadPorts()
        {
            foreverComboBox_port.Items.AddRange(SerialPort.GetPortNames());
        }
        private void LoadFromJson()
        {
            string filePath = "SetLog.json";
            if (!File.Exists(filePath))
                return;

            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data == null)
                return;

            if (data.TryGetValue("crownTextBox_save", out object value1))
            {
                crownTextBox_save.Text = value1.ToString();
                savePath = crownTextBox_save.Text;
            }
            if (data.TryGetValue("foreverComboBox_rate", out object value2))
            {
                foreverComboBox_rate.SelectedIndex = int.Parse(value2.ToString());
                updateSaveRate(); // 更新保存频率
            }
        }

        #region 操作栏
        private void button1_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择一个文件夹";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    crownTextBox_save.Text = dialog.SelectedPath;
                }
            }
        }

        private void crownTextBox_save_TextChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[crownTextBox_save.Name] = crownTextBox_save.Text;
            data[foreverComboBox_rate.Name] = foreverComboBox_rate.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("SetLog.json", json);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                guiyihua = true;
            }
            else
            {
                guiyihua = false;
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            List<string> selected12 = uCheckComboBox1.GetSelectedTexts();

            // 绘制 Sensor 12 (formsPlot1)
            RedrawSelectedChannels(formsPlot1, selected12, 12);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            List<string> selected32 = uCheckComboBox2.GetSelectedTexts();

            // 绘制 Sensor 32 (formsPlot2)
            RedrawSelectedChannels(formsPlot2, selected32, 32);
        }

        private void foreverComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (foreverComboBox_rate.SelectedItem.ToString())
            {
                case "100Hz":
                    saveRate = 10;
                    break;
                case "50Hz":
                    saveRate = 20;
                    break;
                case "20Hz":
                    saveRate = 50;
                    break;
                case "10Hz":
                    saveRate = 100;
                    break;
                case "1Hz":
                    saveRate = 1000;
                    break;
                case "0.1Hz":
                    saveRate = 10000;
                    break;
                case "1/60Hz":
                    saveRate = 60000;
                    break;
                default:
                    saveRate = 1000;
                    break;
            }

            var data = new Dictionary<string, object>();

            data[crownTextBox_save.Name] = crownTextBox_save.Text;
            data[foreverComboBox_rate.Name] = foreverComboBox_rate.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("SetLog.json", json);
        }
        private void updateSaveRate()
        {
            switch (foreverComboBox_rate.SelectedItem.ToString())
            {
                case "100Hz":
                    saveRate = 10;
                    break;
                case "50Hz":
                    saveRate = 20;
                    break;
                case "20Hz":
                    saveRate = 50;
                    break;
                case "10Hz":
                    saveRate = 100;
                    break;
                case "1Hz":
                    saveRate = 1000;
                    break;
                case "0.1Hz":
                    saveRate = 10000;
                    break;
                case "1/60Hz":
                    saveRate = 60000;
                    break;
                default:
                    saveRate = 1000;
                    break;
            }
        }
        #endregion

        #region 串口
        private SerialPort serialPort = new SerialPort();
        private StreamWriter packetWriter12;
        private StreamWriter packetWriter32;
        private Thread serialThread;
        private Thread serialSendThread;
        private CancellationTokenSource cts;
        private List<string> biaoTouName12 = new List<string>();
        private List<string> biaoTouName32 = new List<string>();
        byte[][] memsCommands = new byte[2][];
        private int s12addr = 1;
        private int s32addr = 2;
        private void button_open_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                if (foreverComboBox_port.SelectedIndex == -1)
                {
                    MessageBox.Show("请选择串口");
                    return;
                }
                if (!checkBox_s12.Checked && !checkBox_s32.Checked)
                {
                    MessageBox.Show("请勾选传感器");
                    return;
                }
                if (crownTextBox_addr12.Text == "" && crownTextBox_addr32.Text == "")
                {
                    MessageBox.Show("请填写地址，S12为奇数，S32为偶数");
                    return;
                }
                s12addr = int.Parse(crownTextBox_addr12.Text);
                s32addr = int.Parse(crownTextBox_addr32.Text);
                bool isEven = true;
                bool isOdd = true;
                if (checkBox_s12.Checked)
                {
                    isOdd = (s12addr & 1) == 1; // 奇数
                }
                if (checkBox_s32.Checked)
                {
                    isEven = (s32addr & 1) == 0; // 偶数
                }

                if (!isEven || !isOdd)
                {
                    MessageBox.Show("请填写地址，S12为奇数，S32为偶数");
                    return;
                }
                // 构建 SerialConfig 对象
                try
                {
                    serialPort.PortName = foreverComboBox_port.SelectedItem.ToString();
                    serialPort.BaudRate = 921600;
                    serialPort.DataBits = 8;
                    serialPort.Parity = Parity.None;
                    serialPort.StopBits = StopBits.One;
                    serialPort.Handshake = Handshake.None;

                    OpenSerialPort();

                    if (serialPort.IsOpen)
                    {
                        label_port.Text = "已连接";
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开串口失败: " + ex.Message);
                    return;
                }
            }
            else
            {
                MessageBox.Show("串口已开启");
            }
        }

        private void button_close_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    MessageBox.Show("请先连接串口！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                //serialPort.Close();
                CloseSerialPort();

                // 关闭12通道文件写入器
                packetWriter12?.Flush();
                packetWriter12?.Close();
                packetWriter12 = null;

                // 关闭32通道文件写入器
                packetWriter32?.Flush();
                packetWriter32?.Close();
                packetWriter32 = null;

                label_port.Text = "未连接";
            }
            catch (Exception ex)
            {
                MessageBox.Show("关闭连接失败，请检查串口状态！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// 打开串口
        /// </summary>
        private void OpenSerialPort()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();

                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();

                guiyihua = checkBox1.Checked;
                List<int> fingerNum = new();

                if (checkBox_s12.Checked)
                {
                    fingerNum.Add(s12addr);
                }

                if (checkBox_s32.Checked)
                {
                    fingerNum.Add(s32addr);
                }

                // 根据真实数量创建 memsCommands
                memsCommands = new byte[fingerNum.Count][];

                for (int i = 0; i < fingerNum.Count; i++)
                {
                    memsCommands[i] = new byte[]
                    {
                        0x7B,
                        0xB7,
                        (byte)fingerNum[i]
                    };
                }


                // 启动后台读取线程
                cts = new CancellationTokenSource();
                serialThread = new Thread(() => SerialReadLoop(cts.Token));
                serialThread.IsBackground = true;
                serialThread.Start();

                serialSendThread = new Thread(() => SerialSendLoop(cts.Token, fingerNum.Count));
                serialSendThread.IsBackground = true;
                serialSendThread.Start();

                savePath = crownTextBox_save.Text;
                
                // 生成12通道文件路径和表头
                if (checkBox_s12.Checked)
                {
                    string fileSavePath12 = System.IO.Path.Combine(savePath,
                        $"Gripper12_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    
                    // 构建12通道表头：LogTime, Sensor, T1-T12, P1-P12
                    biaoTouName12.Clear();
                    biaoTouName12.Add("LogTime");
                    biaoTouName12.Add("Sensor");
                    for (int i = 1; i <= 12; i++)
                    {
                        biaoTouName12.Add($"T{i}");
                    }
                    for (int i = 1; i <= 12; i++)
                    {
                        biaoTouName12.Add($"P{i}");
                    }
                    
                    // 创建12通道 StreamWriter 并写入表头
                    packetWriter12 = new StreamWriter(fileSavePath12, true, new System.Text.UTF8Encoding(false));
                    packetWriter12.WriteLine(string.Join(",", biaoTouName12));
                    packetWriter12.AutoFlush = true;
                }

                // 生成32通道文件路径和表头
                if (checkBox_s32.Checked)
                {
                    string fileSavePath32 = System.IO.Path.Combine(savePath,
                        $"Gripper32_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    
                    // 构建32通道表头：LogTime, Sensor, T1-T32, P1-P32
                    biaoTouName32.Clear();
                    biaoTouName32.Add("LogTime");
                    biaoTouName32.Add("Sensor");
                    for (int i = 1; i <= 32; i++)
                    {
                        biaoTouName32.Add($"T{i}");
                    }
                    for (int i = 1; i <= 32; i++)
                    {
                        biaoTouName32.Add($"P{i}");
                    }
                    
                    // 创建32通道 StreamWriter 并写入表头
                    packetWriter32 = new StreamWriter(fileSavePath32, true, new System.Text.UTF8Encoding(false));
                    packetWriter32.WriteLine(string.Join(",", biaoTouName32));
                    packetWriter32.AutoFlush = true;
                }

                StartWorkers();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开串口失败: " + ex.Message);
            }
        }
        /// <summary>
        /// 关闭串口
        /// </summary>
        private void CloseSerialPort()
        {
            try
            {
                if (cts != null)
                {
                    cts.Cancel();
                    Thread.Sleep(100);
                }

                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show("关闭串口失败: " + ex.Message);
            }
        }
        #endregion

        #region 文件存储线程
        private readonly BlockingCollection<string> fileQueue12 = new BlockingCollection<string>(new ConcurrentQueue<string>(), 20000);
        private readonly BlockingCollection<string> fileQueue32 = new BlockingCollection<string>(new ConcurrentQueue<string>(), 20000);
        private readonly BlockingCollection<List<string>> fileRawQueue = new BlockingCollection<List<string>>(new ConcurrentQueue<List<string>>(), 20000);
        private Thread fileWriterThread12;
        private Thread fileWriterThread32;
        private Thread formatThread;
        private object saveLock12 = new object();
        private object saveLock32 = new object();
        private long totalPacketCount = 0;
        private long savedPacketCount12 = 0;
        private long savedPacketCount32 = 0;

        private void StartWorkers()
        {
            // 启动格式化工人线程
            formatThread = new Thread(FormatWorkerLoop) { IsBackground = true, Name = "FormatWorker" };
            formatThread.Start();

            // 启动两个独立的文件写入线程
            fileWriterThread12 = new Thread(() => FileWriterLoop12()) 
                { IsBackground = true, Name = "FileWriter12" };
            fileWriterThread12.Start();

            fileWriterThread32 = new Thread(() => FileWriterLoop32()) 
                { IsBackground = true, Name = "FileWriter32" };
            fileWriterThread32.Start();
        }
        private void FileWriterLoop12()
        {
            try
            {
                while (!fileQueue12.IsCompleted)
                {
                    var batch = new List<string>();
                    while (fileQueue12.TryTake(out var line))
                        batch.Add(line);

                    if (batch.Count > 0 && packetWriter12 != null)
                    {
                        lock (saveLock12)
                        {
                            foreach (var line in batch)
                            {
                                packetWriter12.WriteLine(line);
                                Interlocked.Increment(ref savedPacketCount12);
                            }
                            packetWriter12.Flush();
                        }

                        // UI 更新（显示两个传感器的总包数）
                        long totalSaved = Interlocked.Read(ref savedPacketCount12) + Interlocked.Read(ref savedPacketCount32);
                        if (label_save.InvokeRequired)
                            label_save.BeginInvoke(new Action(() =>
                                label_save.Text = $"已存包数: {totalSaved} (S12:{savedPacketCount12}, S32:{savedPacketCount32})"));
                        else
                            label_save.Text = $"已存包数: {totalSaved} (S12:{savedPacketCount12}, S32:{savedPacketCount32})";
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                // 工业级：文件写线程异常时记录但不弹窗（避免阻塞）
                SafeLogger.LogException("文件写线程12异常", ex);
            }
        }
        private void FileWriterLoop32()
        {
            try
            {
                while (!fileQueue32.IsCompleted)
                {
                    var batch = new List<string>();
                    while (fileQueue32.TryTake(out var line))
                        batch.Add(line);

                    if (batch.Count > 0 && packetWriter32 != null)
                    {
                        lock (saveLock32)
                        {
                            foreach (var line in batch)
                            {
                                packetWriter32.WriteLine(line);
                                Interlocked.Increment(ref savedPacketCount32);
                            }
                            packetWriter32.Flush();
                        }

                        // UI 更新（显示两个传感器的总包数）
                        long totalSaved = Interlocked.Read(ref savedPacketCount12) + Interlocked.Read(ref savedPacketCount32);
                        if (label_save.InvokeRequired)
                            label_save.BeginInvoke(new Action(() =>
                                label_save.Text = $"已存包数: {totalSaved} (S12:{savedPacketCount12}, S32:{savedPacketCount32})"));
                        else
                            label_save.Text = $"已存包数: {totalSaved} (S12:{savedPacketCount12}, S32:{savedPacketCount32})";
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                // 工业级：文件写线程异常时记录但不弹窗（避免阻塞）
                SafeLogger.LogException("文件写线程32异常", ex);
            }
        }
        private void FormatWorkerLoop()
        {
            try
            {
                foreach (var packet in fileRawQueue.GetConsumingEnumerable())
                {
                    if (packet == null || packet.Count == 0) continue;

                    // 根据第一个元素判断是12通道还是32通道
                    string sensorType = packet[0];
                    string line = FormatPacketToOneCsvLineFast(packet);

                    if (line == null) continue;

                    // 根据传感器类型分别放入不同的队列
                    if (sensorType == "S12")
                    {
                        if (fileQueue12.Count >= 20000) fileQueue12.TryTake(out _);
                        fileQueue12.Add(line);
                    }
                    else if (sensorType == "S32")
                    {
                        if (fileQueue32.Count >= 20000) fileQueue32.TryTake(out _);
                        fileQueue32.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogger.LogException("[ERR] FormatWorker", ex);
            }
        }
        [ThreadStatic] private static StringBuilder _sbCache;
        private string FormatPacketToOneCsvLineFast(List<string> packet)
        {
            if (packet == null) return null;

            // 初始化缓存（只在第一次调用时分配）
            if (_sbCache == null) _sbCache = new StringBuilder(4096);

            // 清空缓存
            _sbCache.Clear();

            // 先写时间戳
            _sbCache.Append(HighResDateTime.Now.ToString("yy:MM:dd:HH:mm:ss.fff"));


            for (int i = 0; i < packet.Count; i++)
            {
                _sbCache.Append(',');
                _sbCache.Append(packet[i]);
            }

            return _sbCache.ToString();
        }
        #endregion

        #region 接收线程
        private readonly object serialLock = new object(); // 锁，保证线程安全
        private byte[] lastValidPacket12 = null;
        private byte[] lastValidPacket32 = null;
        // 错误数据包计数，用于检测连续错误
        private int consecutiveErrorPackets = 0;
        private const int MaxConsecutiveErrors = 10; // 连续10个错误包后可能需要重置
        public class ListStringPool
        {
            private readonly ConcurrentBag<List<string>> pool = new ConcurrentBag<List<string>>();

            public List<string> Rent()
            {
                if (pool.TryTake(out var list))
                {
                    list.Clear();
                    return list;
                }
                return new List<string>(50);
            }

            public void Return(List<string> list)
            {
                list.Clear();
                pool.Add(list);
            }
        }
        private static ListStringPool uiDataPool = new ListStringPool();
        private static ListStringPool fileDataPool = new ListStringPool();
        // 工业级优化：增大队列容量，减少丢包率（从200增加到2000）
        private const int MaxQueueSize = 2000;
        private int MaxVisiblePackets = 200;
        private BlockingCollection<List<string>> uiQueue = new BlockingCollection<List<string>>(MaxQueueSize);

        // 丢包统计（工业级监控）
        private long droppedPacketsCount = 0;
        private long totalEnqueuedPackets = 0;
        private readonly object dropStatsLock = new object();
        // 创建全局字典来存储每个 addr 对应的温度和压力数据
        public struct SensorData
        {
            public List<double> TemperatureData;
            public List<double> PressureData;

            public SensorData()
            {
                TemperatureData = new List<double>();
                PressureData = new List<double>();
            }
        }
        private ConcurrentDictionary<int, SensorData> addrDataDict12 = new ConcurrentDictionary<int, SensorData>();
        private ConcurrentDictionary<int, SensorData> addrDataDict32 = new ConcurrentDictionary<int, SensorData>();
        private Dictionary<int, Queue<double>> channelBuffers12 = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> channelBuffers_filedata12 = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> channelBuffers_temp12 = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> channelBuffers32 = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> channelBuffers_filedata32 = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> channelBuffers_temp32 = new Dictionary<int, Queue<double>>();
        private double[] channelZeroOffsets12 = new double[12];
        private readonly int[] channelZeroingCounts12 = new int[12];
        private double[] channelZeroOffsets32 = new double[32];
        private readonly int[] channelZeroingCounts32 = new int[32];
        private volatile bool isSaving12 = false;
        private volatile bool isSaving32 = false;
        private DateTime lastSaveTime = DateTime.Now;
        private DateTime lastUIUpdateTime = DateTime.Now;
        private const int UIUpdateIntervalMs = 50; // UI 更新间隔（毫秒），减少更新频率

        /*        private void SerialSendLoop(CancellationToken token, int count)
                {
                    int memsSensorIndex = 0;
                    double pollIntervalMs = Math.Max(2.0, 20.0 / count);


                    while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
                    {
                        try
                        {
                            int attempts = 0;
                            while (memsCommands[memsSensorIndex] == null && attempts < memsCommands.Length)
                            {
                                memsSensorIndex = (memsSensorIndex + 1) % memsCommands.Length;
                                attempts++;
                            }

                            if (memsCommands[memsSensorIndex] != null)
                            {
                                serialPort.Write(memsCommands[memsSensorIndex], 0, memsCommands[memsSensorIndex].Length);
                            }

                            memsSensorIndex = (memsSensorIndex + 1) % memsCommands.Length;

                            Thread.Sleep((int)pollIntervalMs); // 用 Sleep 控制间隔
                        }
                        catch (Exception ex)
                        {
                            SafeLogger.LogException("SerialSendLoop", ex);
                        }
                    }
                }*/
        private void SerialSendLoop(CancellationToken token, int count)
        {
            double TargetHzPerSensor = 50.0;
            if(int.TryParse(crownTextBox_sendRate.Text, out int valueR))
            {
                TargetHzPerSensor = valueR;
            }
            double pollIntervalMs = Math.Max(1.0, 1000.0 / (count * TargetHzPerSensor));

            int memsSensorIndex = 0;

            // 工业级优化：使用 Stopwatch 代替 Thread.Sleep 来实现精确延时和补偿
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            long targetTicks = (long)(pollIntervalMs * System.Diagnostics.Stopwatch.Frequency / 1000.0);

            stopwatch.Start();

            while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
            {
                // 记录本轮发送开始的时间
                long startTime = stopwatch.ElapsedTicks;

                try
                {
                    // --- 轮询逻辑 (保持不变) ---
                    int attempts = 0;
                    while (memsCommands[memsSensorIndex] == null && attempts < memsCommands.Length)
                    {
                        memsSensorIndex = (memsSensorIndex + 1) % memsCommands.Length;
                        attempts++;
                    }

                    if (memsCommands[memsSensorIndex] != null)
                    {
                        serialPort.Write(memsCommands[memsSensorIndex], 0, memsCommands[memsSensorIndex].Length);
                    }

                    memsSensorIndex = (memsSensorIndex + 1) % memsCommands.Length;

                    // --- 精确延时和时间补偿 ---
                    long elapsedTicks = stopwatch.ElapsedTicks - startTime;
                    long remainingTicks = targetTicks - elapsedTicks;

                    if (remainingTicks > 0)
                    {
                        // 将剩余的 Tick 转换为毫秒并进行 Sleep
                        double remainingMs = remainingTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                        if (remainingMs > 1.0) // 确保 Sleep 时间足够长
                        {
                            Thread.Sleep((int)remainingMs);
                        }
                        else if (remainingMs > 0)
                        {
                            // 对于极短的剩余时间，使用 Thread.SpinWait 或直接空转等待
                            // C# 中 Thread.Sleep(0) 或 SpinWait 都可以用来提高精度
                            // 对于 10ms 的周期，直接使用 Thread.Sleep(int) 已经足够
                            // 为了简单和兼容性，继续使用 Sleep，但如果需要极高精度，应使用 SpinWait/BusyWait
                        }
                    }
                    // 如果 elapsedTicks >= targetTicks，说明本轮发送耗时太长，无需 Sleep，下一轮将立即开始 (时间补偿)

                }
                catch (Exception ex)
                {
                    SafeLogger.LogException("SerialSendLoop", ex);
                }
            }
        }
        /*        private void SerialReadLoop(CancellationToken token)
                {
                    byte[] buffer = new byte[4096];
                    const int MaxBufferSize = 65536;
                    byte[] recvBuffer = new byte[MaxBufferSize];
                    int recvHead = 0; // 有效数据起始
                    int recvTail = 0; // 有效数据末尾

                    ArrayPool<byte> pool = ArrayPool<byte>.Shared;

                    while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
                    {
                        try
                        {
                            // === 串口接收 ===
                            int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                            if (bytesRead <= 0) continue;

                            // 工业级优化：减少锁持有时间，先快速复制数据到环形缓冲区
                            int newTail;
                            lock (serialLock)
                            {
                                // 检查缓冲区空间，防止溢出
                                int availableSpace = (recvHead - recvTail - 1 + MaxBufferSize) % MaxBufferSize;
                                if (availableSpace < bytesRead)
                                {
                                    // 缓冲区满，丢弃最旧的数据
                                    int overflow = bytesRead - availableSpace;
                                    recvHead = (recvHead + overflow) % MaxBufferSize;
                                    Interlocked.Increment(ref droppedPacketsCount);
                                }

                                // 批量复制数据到环形缓冲区（比逐个字节快得多）
                                int firstPart = Math.Min(bytesRead, MaxBufferSize - recvTail);
                                Buffer.BlockCopy(buffer, 0, recvBuffer, recvTail, firstPart);
                                if (bytesRead > firstPart)
                                {
                                    Buffer.BlockCopy(buffer, firstPart, recvBuffer, 0, bytesRead - firstPart);
                                }
                                newTail = (recvTail + bytesRead) % MaxBufferSize;
                                recvTail = newTail;
                            }

                            // 修复：恢复原来的逻辑，在锁内完成数据包提取和校验，确保数据包正确入队
                            lock (serialLock)
                            {
                                while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= 6)
                                {
                                    if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0x42 &&
                                            PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0x54))
                                    {
                                        recvHead = (recvHead + 1) % MaxBufferSize;
                                        continue;
                                    }

                                    int length = PeekByte(recvBuffer, recvHead, 2, MaxBufferSize);
                                    // 工业级：增加长度验证，防止异常数据（放宽上限，避免过滤正常数据包）
                                    if (length < 6 || length > 2048 || GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < length)
                                    {
                                        recvHead = (recvHead + 1) % MaxBufferSize;
                                        continue;
                                    }

                                    byte[] packet = pool.Rent(length);
                                    CopyFromRingBuffer(recvBuffer, recvHead, packet, length, MaxBufferSize);
                                    recvHead = (recvHead + length) % MaxBufferSize;

                                    // 在锁内进行校验和计算（保持原子性）
                                    byte checksum = 0;
                                    var packetSpan = packet.AsSpan(2, length - 3);
                                    foreach (byte b in packetSpan)
                                    {
                                        checksum += b;
                                    }

                                    if (checksum == packet[length - 1])
                                    {
                                        // 校验通过，在锁外入队（EnqueuePacket 内部可能有其他操作）
                                        EnqueuePacket(packet);
                                    }
                                    else
                                    {
                                        // 校验失败，归还内存
                                        pool.Return(packet);
                                    }
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                            // 超时是正常情况，继续循环
                        }
                        catch (IOException ex)
                        {
                            // 工业级：IO异常时记录并退出
                            SafeLogger.LogException("串口IO异常", ex);
                            break;
                        }
                        catch (InvalidOperationException ex)
                        {
                            // 串口关闭等操作异常
                            SafeLogger.LogException("串口操作异常", ex);
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message != "The operation was canceled.")
                            {
                                SafeLogger.LogException("串口读取异常", ex);
                            }
                        }
                    }
                }*/
        private void SerialReadLoop(CancellationToken token)
        {
            const int MaxBufferSize = 65536;
            byte[] recvBuffer = new byte[MaxBufferSize];
            int recvHead = 0; // 有效数据起始
            int recvTail = 0; // 有效数据末尾
            byte[] tempBuffer = new byte[4096];

            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    int bytesRead = serialPort.Read(tempBuffer, 0, tempBuffer.Length);
                    if (bytesRead <= 0) continue;

                    lock (serialLock)
                    {
                        // 写入环形缓冲区
                        int availableSpace = (recvHead - recvTail - 1 + MaxBufferSize) % MaxBufferSize;
                        if (availableSpace < bytesRead)
                        {
                            int overflow = bytesRead - availableSpace;
                            recvHead = (recvHead + overflow) % MaxBufferSize;
                            Interlocked.Increment(ref droppedPacketsCount);
                        }

                        int firstPart = Math.Min(bytesRead, MaxBufferSize - recvTail);
                        Buffer.BlockCopy(tempBuffer, 0, recvBuffer, recvTail, firstPart);
                        if (bytesRead > firstPart)
                            Buffer.BlockCopy(tempBuffer, firstPart, recvBuffer, 0, bytesRead - firstPart);
                        recvTail = (recvTail + bytesRead) % MaxBufferSize;
                    }

                    // ---- 解析环形缓冲区数据 ----
                    lock (serialLock)
                    {
                        while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= 6)
                        {
                            // 找包头
                            if (PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) != 0x42 ||
                                PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) != 0x54)
                            {
                                recvHead = (recvHead + 1) % MaxBufferSize;
                                continue;
                            }

                            int length = PeekByte(recvBuffer, recvHead, 2, MaxBufferSize);

                            // 核心改动：半帧不丢，等待下一次接收
                            if (length < 6 || length > 2048)
                            {
                                recvHead = (recvHead + 1) % MaxBufferSize;
                                continue;
                            }

                            int availableBytes = GetAvailableBytes(recvHead, recvTail, MaxBufferSize);
                            if (availableBytes < length)
                            {
                                // 半帧，退出循环等待下一次 Read
                                break;
                            }

                            // 拷贝完整帧
                            byte[] packet = pool.Rent(length);
                            CopyFromRingBuffer(recvBuffer, recvHead, packet, length, MaxBufferSize);
                            recvHead = (recvHead + length) % MaxBufferSize;

                            // 严格验证：检查数据包长度是否与实际内容匹配
                            if (packet.Length < length || length < 6)
                            {
                                pool.Return(packet);
                                continue;
                            }

                            // 校验和
                            byte checksum = 0;
                            var packetSpan = packet.AsSpan(2, length - 3);
                            foreach (byte b in packetSpan)
                                checksum += b;

                            if (checksum == packet[length - 1])
                            {
                                // 校验通过，重置错误计数
                                consecutiveErrorPackets = 0;
                                // 校验通过，但需要进一步验证数据包完整性
                                // 在 EnqueuePacket 中会进行更详细的验证
                                EnqueuePacket(packet);
                            }
                            else
                            {
                                // 校验失败，记录并丢弃
                                consecutiveErrorPackets++;
                                if (consecutiveErrorPackets >= MaxConsecutiveErrors)
                                {
                                    SafeLogger.LogException("连续校验失败", new Exception($"连续 {consecutiveErrorPackets} 个数据包校验失败，可能存在数据同步问题"));
                                    consecutiveErrorPackets = 0; // 重置计数，避免日志刷屏
                                }
                                pool.Return(packet);
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (IOException ex)
                {
                    SafeLogger.LogException("串口IO异常", ex);
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    SafeLogger.LogException("串口操作异常", ex);
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.Message != "The operation was canceled.")
                        SafeLogger.LogException("串口读取异常", ex);
                }
            }
        }


        /// <summary> 计算环形缓冲区可用字节数 </summary>
        private static int GetAvailableBytes(int head, int tail, int capacity)
        {
            return (tail - head + capacity) % capacity;
        }

        /// <summary> 从环形缓冲区读取一个字节 </summary>
        private static byte PeekByte(byte[] buffer, int head, int offset, int capacity)
        {
            return buffer[(head + offset) % capacity];
        }

        /// <summary> 拷贝环形缓冲区到数组 </summary>
        private static void CopyFromRingBuffer(byte[] ring, int head, byte[] dest, int length, int capacity)
        {
            int firstPart = Math.Min(length, capacity - head);
            Buffer.BlockCopy(ring, head, dest, 0, firstPart);
            if (length > firstPart)
            {
                Buffer.BlockCopy(ring, 0, dest, firstPart, length - firstPart);
            }
        }

        private void EnqueuePacket(byte[] packet)
        {
            try
            {
                if (packet == null || packet.Length < 10) return;

                int length = packet[2];
                byte addr = packet[3];
                byte type = packet[4];
                
                // 严格验证：数据包长度必须与实际数组长度一致
                if (packet.Length < length)
                {
                    SafeLogger.LogException("数据包长度不匹配", new Exception($"声明长度: {length}, 实际长度: {packet.Length}"));
                    return;
                }
                
                if (addr != 1 && addr != 2) return;
                
                // 根据类型和地址验证数据包长度
                int expectedLength = 0;
                if (addr == 1) // S12
                {
                    if (type == 0xF4) // 温度：包头(3) + 地址(1) + 类型(1) + 其他头部(8) + 数据(12*2) + 校验(1) = 38
                        expectedLength = 3 + 1 + 1 + 8 + 12 * 2 + 1;
                    else if (type == 0xF5) // 压力：包头(3) + 地址(1) + 类型(1) + 其他头部(8) + 数据(12*4) + 校验(1) = 62
                        expectedLength = 3 + 1 + 1 + 8 + 12 * 4 + 1;
                }
                else if (addr == 2) // S32
                {
                    if (type == 0xF4) // 温度：包头(3) + 地址(1) + 类型(1) + 其他头部(8) + 数据(32*2) + 校验(1) = 78
                        expectedLength = 3 + 1 + 1 + 8 + 32 * 2 + 1;
                    else if (type == 0xF5) // 压力：包头(3) + 地址(1) + 类型(1) + 其他头部(8) + 数据(32*4) + 校验(1) = 142
                        expectedLength = 3 + 1 + 1 + 8 + 32 * 4 + 1;
                }
                
                // 验证数据包长度是否匹配（允许±2的误差，因为可能有其他字段）
                if (expectedLength > 0 && Math.Abs(length - expectedLength) > 2)
                {
                    // 长度不匹配时，记录但不立即返回，因为可能数据包格式有变化
                    // 但会在后续的数据读取时进行更严格的验证
                    SafeLogger.LogException("数据包长度验证警告", new Exception($"地址: {addr}, 类型: 0x{type:X2}, 期望长度: {expectedLength}, 实际长度: {length}"));
                    // 不返回，继续处理，让后续的数据读取验证来过滤
                }

                if (addr == 1)
                {
                    // 复用数组
                    Span<double> values = stackalloc double[12];

                    int dataOffset = 13;

                    // 如果是温度数据 (F4)
                    if (type == 0xF4)
                    {
                        // 验证数据长度是否足够
                        int requiredLength = dataOffset + 12 * 2;
                        if (packet.Length < requiredLength)
                        {
                            SafeLogger.LogException("温度数据包长度不足", new Exception($"需要: {requiredLength}, 实际: {packet.Length}"));
                            return;
                        }
                        
                        for (int i = 0; i < 12; i++)
                        {
                            if (dataOffset + i * 2 + 1 >= packet.Length) break;
                            double v = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(dataOffset + i * 2, 2));
                            values[i] = v / 10;
                        }

                        // 优化：使用 GetOrAdd 减少字典查找次数
                        var sensorData12 = addrDataDict12.GetOrAdd(addr, _ => new SensorData());

                        // 只添加新的数据，避免多次添加
                        if (sensorData12.TemperatureData.Count < 12)
                        {
                            var tempList = sensorData12.TemperatureData;
                            for (int i = 0; i < 12; i++)
                            {
                                tempList.Add(values[i]);
                            }
                        }
                    }
                    // 如果是压力数据 (F5)
                    else if (type == 0xF5)
                    {
                        // 验证数据长度是否足够
                        int requiredLength = dataOffset + 12 * 4;
                        if (packet.Length < requiredLength)
                        {
                            SafeLogger.LogException("压力数据包长度不足", new Exception($"需要: {requiredLength}, 实际: {packet.Length}"));
                            return;
                        }
                        
                        for (int i = 0; i < 12; i++)
                        {
                            if (dataOffset + i * 4 + 3 >= packet.Length) break;
                            // 修复：使用 BinaryPrimitives 确保小端序读取
                            double v = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(dataOffset + i * 4, 4));
                            if (guiyihua)
                            {
                                v = v / 1000.0; // Kpa

                                // 保证最小值为 1
                                if (v > 0 && v < 1) v = 1;
                                if (v < 0 && v > -1) v = -1;
                            }

                            values[i] = v;
                        }

                        // 优化：使用 GetOrAdd 减少字典查找次数
                        var sensorData12_pres = addrDataDict12.GetOrAdd(addr, _ => new SensorData());

                        // 只添加新的数据，避免多次添加
                        if (sensorData12_pres.PressureData.Count < 12)
                        {
                            var presList = sensorData12_pres.PressureData;
                            for (int i = 0; i < 12; i++)
                            {
                                presList.Add(values[i]);
                            }
                        }
                    }
                    else
                    {
                        return;
                    }

                    lastValidPacket12 = packet;

                    // 获取 List<string> 对象池
                    var uiData = uiDataPool.Rent();
                    uiData.Clear();
                    uiData.Add("S12");
                    uiData.Add(type.ToString("X2"));
                    for (int i = 0; i < 12; i++)
                        uiData.Add(values[i].ToString());

                    // 工业级优化：智能队列管理，优先保证数据完整性
                    if (uiQueue.Count >= MaxQueueSize)
                    {
                        // 队列满时，尝试丢弃最旧的数据
                        if (uiQueue.TryTake(out var dropped))
                        {
                            Interlocked.Increment(ref droppedPacketsCount);
                            // 归还对象池，防止内存泄漏
                            if (dropped != null) uiDataPool.Return(dropped);
                        }
                    }
                    Interlocked.Increment(ref totalEnqueuedPackets);
                    uiQueue.Add(uiData);

                    // 检查该 addr 是否有足够的数据（温度和压力数据各 12 个）
                    var finalData12 = addrDataDict12.GetOrAdd(addr, _ => new SensorData());
                    if (finalData12.TemperatureData.Count >= 12 && finalData12.PressureData.Count >= 12)
                    {
                        // 当数据满足条件时，加入 fileRawQueue
                        var fileData = uiDataPool.Rent();
                        fileData.Clear();
                        fileData.Add("S12");

                        // 将温度数据和压力数据一起添加到 uiData
                        for (int i = 0; i < 12; i++)
                        {
                            double rawV = finalData12.TemperatureData[i];
                            int channelIndex = i;
                            double filedV = HampelFilter(channelIndex, rawV, channelBuffers_temp12);
                            fileData.Add(filedV.ToString("F2"));
                        }
                        for (int i = 0; i < 12; i++)
                        {
                            double rawV = finalData12.PressureData[i];
                            int channelIndex = i;
                            double zeroedV = rawV - channelZeroOffsets12[channelIndex];
                            double filedV = HampelFilter(channelIndex, zeroedV, channelBuffers_filedata12);
                            fileData.Add(filedV.ToString("F3"));
                        }
                        if (isSaving12)
                        {
                            // 保存数据到 fileRawQueue
                            var nowT = HighResDateTime.Now;
                            if ((nowT - lastSaveTime).TotalMilliseconds >= saveRate)
                            {
                                lastSaveTime = nowT;
                                if (fileRawQueue.Count >= 20000) fileRawQueue.TryTake(out _);
                                fileRawQueue.Add(fileData);
                            }
                        }
                        // 清除该 addr 的数据（温度和压力都清除）
                        addrDataDict12[addr] = new SensorData();
                    }

                    Interlocked.Increment(ref totalPacketCount);
                }

                if (addr == 2)
                {
                    // 复用数组
                    Span<double> values = stackalloc double[32];

                    int dataOffset = 13;

                    // 如果是温度数据 (F4)
                    if (type == 0xF4)
                    {
                        // 验证数据长度是否足够
                        int requiredLength = dataOffset + 32 * 2;
                        if (packet.Length < requiredLength)
                        {
                            SafeLogger.LogException("温度数据包长度不足", new Exception($"需要: {requiredLength}, 实际: {packet.Length}"));
                            return;
                        }
                        
                        for (int i = 0; i < 32; i++)
                        {
                            if (dataOffset + i * 2 + 1 >= packet.Length) break;
                            double v = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(dataOffset + i * 2, 2));
                            values[i] = v / 10;
                        }

                        // 优化：使用 GetOrAdd 减少字典查找次数
                        var sensorData32 = addrDataDict32.GetOrAdd(addr, _ => new SensorData());

                        // 只添加新的数据，避免多次添加
                        if (sensorData32.TemperatureData.Count < 32)
                        {
                            var tempList = sensorData32.TemperatureData;
                            for (int i = 0; i < 32; i++)
                            {
                                tempList.Add(values[i]);
                            }

                        }
                    }
                    // 如果是压力数据 (F5)
                    else if (type == 0xF5)
                    {
                        // 验证数据长度是否足够
                        int requiredLength = dataOffset + 32 * 4;
                        if (packet.Length < requiredLength)
                        {
                            SafeLogger.LogException("压力数据包长度不足", new Exception($"需要: {requiredLength}, 实际: {packet.Length}"));
                            return;
                        }
                        
                        for (int i = 0; i < 32; i++)
                        {
                            if (dataOffset + i * 4 + 3 >= packet.Length) break;
                            // 修复：使用 BinaryPrimitives 确保小端序读取
                            double v = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(dataOffset + i * 4, 4));
                            if (guiyihua)
                            {
                                v = v / 1000.0; // Kpa

                                // 保证最小值为 1
                                if (v > 0 && v < 1) v = 1;
                                if (v < 0 && v > -1) v = -1;
                            }

                            values[i] = v;
                        }

                        // 优化：使用 GetOrAdd 减少字典查找次数
                        var sensorData32_pres = addrDataDict32.GetOrAdd(addr, _ => new SensorData());

                        // 只添加新的数据，避免多次添加
                        if (sensorData32_pres.PressureData.Count < 32)
                        {
                            var presList = sensorData32_pres.PressureData;
                            for (int i = 0; i < 32; i++)
                            {
                                presList.Add(values[i]);
                            }

                        }
                    }
                    else
                    {
                        return;
                    }

                    lastValidPacket32 = packet;

                    // 获取 List<string> 对象池
                    var uiData = uiDataPool.Rent();
                    uiData.Clear();
                    uiData.Add("S32");
                    uiData.Add(type.ToString("X2"));
                    for (int i = 0; i < 32; i++)
                        uiData.Add(values[i].ToString());

                    // 工业级优化：智能队列管理，优先保证数据完整性
                    if (uiQueue.Count >= MaxQueueSize)
                    {
                        // 队列满时，尝试丢弃最旧的数据
                        if (uiQueue.TryTake(out var dropped))
                        {
                            Interlocked.Increment(ref droppedPacketsCount);
                            // 归还对象池，防止内存泄漏
                            if (dropped != null) uiDataPool.Return(dropped);
                        }
                    }
                    Interlocked.Increment(ref totalEnqueuedPackets);
                    uiQueue.Add(uiData);

                    // 检查该 addr 是否有足够的数据（温度和压力数据各 32 个）
                    var finalData32 = addrDataDict32.GetOrAdd(addr, _ => new SensorData());
                    if (finalData32.TemperatureData.Count >= 32 && finalData32.PressureData.Count >= 32)
                    {
                        // 当数据满足条件时，加入 fileRawQueue
                        var fileData = uiDataPool.Rent();
                        fileData.Clear();
                        fileData.Add("S32");

                        // 将温度数据和压力数据一起添加到 uiData
                        for (int i = 0; i < 32; i++)
                        {
                            double rawV = finalData32.TemperatureData[i];
                            int channelIndex = i;
                            double filedV = HampelFilter(channelIndex, rawV, channelBuffers_temp32);
                            fileData.Add(filedV.ToString("F2"));
                        }
                        for (int i = 0; i < 32; i++)
                        {
                            double rawV = finalData32.PressureData[i];
                            int channelIndex = i;
                            double zeroedV = rawV - channelZeroOffsets32[channelIndex];
                            double filedV = HampelFilter(channelIndex, zeroedV, channelBuffers_filedata32);
                            fileData.Add(filedV.ToString("F3"));
                        }
                        if (isSaving32)
                        {
                            // 保存数据到 fileRawQueue
                            var nowT = HighResDateTime.Now;
                            if ((nowT - lastSaveTime).TotalMilliseconds >= saveRate)
                            {
                                lastSaveTime = nowT;
                                if (fileRawQueue.Count >= 20000) fileRawQueue.TryTake(out _);
                                fileRawQueue.Add(fileData);
                            }
                        }
                        // 清除该 addr 的数据（温度和压力都清除）
                        addrDataDict32[addr] = new SensorData();
                    }

                    Interlocked.Increment(ref totalPacketCount);
                }

                // 优化：限流 UI 更新，减少不必要的界面刷新
                var now = DateTime.Now;
                if ((now - lastUIUpdateTime).TotalMilliseconds >= UIUpdateIntervalMs)
                {
                    lastUIUpdateTime = now;
                    long currentCount = Interlocked.Read(ref totalPacketCount);
                    long droppedCount = Interlocked.Read(ref droppedPacketsCount);
                    long enqueuedCount = Interlocked.Read(ref totalEnqueuedPackets);

                    // 计算丢包率（百分比）
                    double dropRate = 0.0;
                    if (enqueuedCount > 0)
                    {
                        dropRate = (double)droppedCount / enqueuedCount * 100.0;
                    }

                    if (label_receive.InvokeRequired)
                    {
                        label_receive.BeginInvoke(new Action(() =>
                        {
                            label_receive.Text = $"接收包数: {currentCount} | 丢包: {droppedCount} ({dropRate:F2}%)";
                        }));
                    }
                    else
                    {
                        label_receive.Text = $"接收包数: {currentCount} | 丢包: {droppedCount} ({dropRate:F2}%)";
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLogger.LogException("EnqueuePacket 异常", ex);
            }
        }

        /// <summary>
        /// [已废弃] 原中值滤波算法 - 仅能处理单个杂峰，无法处理连续杂峰
        /// 已替换为 HampelFilter 算法
        /// </summary>
        /*
        private double DenoiseByMedian(int channelIndex, double newValue, Dictionary<int, Queue<double>> channelBuffers)
        {
            Dictionary<int, Queue<double>> channelBuf = channelBuffers;
            if (!channelBuf.ContainsKey(channelIndex))
                channelBuf[channelIndex] = new Queue<double>();

            var buffer = channelBuf[channelIndex];

            // 添加新值到队列
            buffer.Enqueue(newValue);

            // 保持队列长度不超过3
            if (buffer.Count > 3)
                buffer.Dequeue();

            // 队列不足3个点，直接返回新值
            if (buffer.Count < 3)
                return newValue;

            // 转成数组，方便索引
            double[] arr = buffer.ToArray(); // [前, 中, 新]
            double prev = arr[0];
            double curr = arr[1];
            double next = arr[2];

            // 自定义阈值（根据实际数据调整）
            double threshold = 1200; // 例如1000，可根据传感器范围调整
            if (guiyihua)
            {
                threshold = 1.2;
            }

            // 判断中间点是否为杂峰：与前后都差距大
            if (Math.Abs(curr - prev) > threshold && Math.Abs(curr - next) > threshold)
            {
                // 杂峰，返回前后平均值
                double denoised = (prev + next) / 2;

                // 替换中间值为平滑值，保证后续判断正确
                buffer.Dequeue();           // 移除最旧值（prev）
                buffer.Dequeue();           // 移除原curr
                buffer.Enqueue(denoised);   // 插入平滑值
                buffer.Enqueue(next);       // 保留最新值
                return denoised;
            }

            // 正常点，直接返回当前值
            return curr;
        }
        */

        /// <summary>
        /// Hampel Filter - 工业级异常值检测与滤波算法
        /// 基于中位数和MAD（中位数绝对偏差）的鲁棒统计方法
        /// 能够有效处理连续多个杂峰点，适用于传感器数据去噪
        /// 
        /// 算法原理：
        /// 1. 使用滑动窗口（默认7点）收集历史数据
        /// 2. 计算窗口内数据的中位数（对异常值鲁棒）
        /// 3. 计算MAD（中位数绝对偏差），比标准差更鲁棒
        /// 4. 使用Z-score方法检测异常值：|x - median| > threshold * MAD
        /// 5. 如果检测到异常值，用中位数替代；否则返回原值
        /// 
        /// 优势：
        /// - 能够处理连续2-3个异常值
        /// - 对异常值不敏感（使用中位数而非均值）
        /// - 自适应阈值（基于数据本身的分布）
        /// - 工业标准算法，广泛应用于传感器数据处理
        /// </summary>
        /// <param name="channelIndex">通道索引</param>
        /// <param name="newValue">新采样值</param>
        /// <param name="channelBuffers">通道缓冲区字典</param>
        /// <returns>滤波后的值</returns>
        private double HampelFilter(int channelIndex, double newValue, Dictionary<int, Queue<double>> channelBuffers)
        {
            Dictionary<int, Queue<double>> channelBuf = channelBuffers;
            if (!channelBuf.ContainsKey(channelIndex))
                channelBuf[channelIndex] = new Queue<double>();

            var buffer = channelBuf[channelIndex];

            // 添加新值到队列
            buffer.Enqueue(newValue);

            // 工业标准：使用7点窗口（可处理连续2-3个异常值）
            // 窗口大小选择：5点（最小），7点（推荐），9点（高噪声环境）
            const int windowSize = 7;
            if (buffer.Count > windowSize)
                buffer.Dequeue();

            // 窗口数据不足时，直接返回新值（避免过度滤波）
            if (buffer.Count < windowSize)
                return newValue;

            // 转换为数组进行处理
            double[] arr = new double[windowSize];
            buffer.CopyTo(arr, 0);
            
            // 创建排序副本用于计算中位数（不修改原数组）
            double[] sorted = new double[windowSize];
            Array.Copy(arr, sorted, windowSize);
            Array.Sort(sorted);

            // 计算中位数（对异常值鲁棒）
            double median = sorted[windowSize / 2];

            // 计算MAD（中位数绝对偏差）
            // MAD = median(|x_i - median|)
            double[] deviations = new double[windowSize];
            for (int i = 0; i < windowSize; i++)
            {
                deviations[i] = Math.Abs(arr[i] - median);
            }
            Array.Sort(deviations);
            double mad = deviations[windowSize / 2];

            // MAD缩放因子：1.4826 使MAD在正态分布下等价于标准差
            // 这是Hampel Filter的标准做法
            const double madScaleFactor = 1.4826;
            double scaledMad = madScaleFactor * mad;

            // 工业标准阈值：3.0（对应99.7%置信区间，3-sigma规则）
            // 可根据实际应用调整：2.5（更敏感）或 3.5（更保守）
            double threshold = 3.0;

            // 对于归一化数据，使用更小的阈值
            if (guiyihua)
            {
                // 归一化数据通常范围较小，使用相对阈值
                threshold = 2.5;
            }

            // 检测中心点（当前点）是否为异常值
            int centerIndex = windowSize / 2;
            double centerValue = arr[centerIndex];
            double zScore = scaledMad > 0 ? Math.Abs(centerValue - median) / scaledMad : 0;

            // 如果Z-score超过阈值，认为是异常值，用中位数替代
            if (zScore > threshold)
            {
                // 异常值：返回中位数（更鲁棒的估计）
                return median;
            }

            // 正常值：返回原值（保持数据真实性）
            return centerValue;
        }
        #endregion

        #region 解析线程
        private bool yalitu = false;
        //private bool wendutu = true;
        private bool diantu = false;
        //private bool yuntu = true;
        int packetIndex12 = 0;
        int packetIndex32 = 0;
        private bool isZeroing12 = false;
        private bool isZeroing32 = false;
        private Dictionary<int, List<double>> pressureCalibBuffers12 = new();
        private Dictionary<int, List<double>> pressureCalibBuffers32 = new();
        private bool[] activeChannels12 = new bool[12];
        private bool[] activeChannels32 = new bool[32];
        private const int ZeroingTargetPackets = 5;
        private DotMatrixUpdate dotUpdate12 = new DotMatrixUpdate();
        private DotMatrixUpdate dotUpdate32 = new DotMatrixUpdate();
        public class GraphUpdate
        {
            public int SensorIndex { get; set; }     // 传感器编号
            public int Channel { get; set; }         // 压力通道编号
            public long Index { get; set; }          // 包序号
            public double Pressure { get; set; }     // 当前压力值
            public double Temperature { get; set; }  // 当前温度值
        }
        class DotMatrixUpdate
        {
            public int SensorIndex;
            public double[] PressureValues;
            public double[] TempValues;

            // 创建深拷贝
            public DotMatrixUpdate Clone()
            {
                return new DotMatrixUpdate
                {
                    SensorIndex = this.SensorIndex,
                    PressureValues = this.PressureValues != null ? (double[])this.PressureValues.Clone() : null,
                    TempValues = this.TempValues != null ? (double[])this.TempValues.Clone() : null
                };
            }
        }
        // 存储解析后的曲线更新数据
        private ConcurrentQueue<GraphUpdate> graphQueue = new ConcurrentQueue<GraphUpdate>();
        private ConcurrentQueue<GraphUpdate> tempQueue = new ConcurrentQueue<GraphUpdate>();
        private ConcurrentQueue<DotMatrixUpdate> dotQueue_Pres = new ConcurrentQueue<DotMatrixUpdate>();
        private ConcurrentQueue<DotMatrixUpdate> dotQueue_Temp = new ConcurrentQueue<DotMatrixUpdate>();

        private void StartPacketProcessingThread()
        {
            Task.Run(() =>
            {
                // 工业级：添加异常恢复机制，确保线程不会因异常而终止
                while (true)
                {
                    try
                    {
                        foreach (var packet in uiQueue.GetConsumingEnumerable())
                        {
                            try
                            {
                                ProcessPacketForUI(packet);
                            }
                            catch (Exception ex)
                            {
                                // 单个数据包处理异常不影响整体运行
                                SafeLogger.LogException("ProcessPacketForUI异常", ex);
                                // 归还对象池，防止内存泄漏
                                if (packet != null) uiDataPool.Return(packet);
                            }
                        }
                        // 如果队列完成，退出循环
                        break;
                    }
                    catch (Exception ex)
                    {
                        // 工业级：严重异常时等待后重试，避免线程终止
                        SafeLogger.LogException("数据包处理线程异常", ex);
                        Thread.Sleep(100); // 等待100ms后重试
                    }
                }
            });
        }

        private void ProcessPacketForUI(List<string> uiData)
        {
            try
            {
                if (uiData[0] == "S12")
                {
                    dotUpdate12.SensorIndex = 12;
                    dotUpdate12.PressureValues = new double[12];
                    dotUpdate12.TempValues = new double[12];

                    string type = uiData[1];

                    if (isZeroing12 && type == "F5")
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            int channelIndex = i;
                            if (channelIndex < 0 || channelIndex >= 12) continue;

                            if (!pressureCalibBuffers12.ContainsKey(channelIndex))
                                pressureCalibBuffers12[channelIndex] = new List<double>();

                            if (double.TryParse(uiData[2 + i], out double pressure))
                            {
                                pressureCalibBuffers12[channelIndex].Add(pressure);
                                channelZeroingCounts12[channelIndex]++;
                                activeChannels12[channelIndex] = true;
                            }
                        }

                        // 判断已激活的通道是否都采满
                        bool allActiveDone = true;
                        for (int ch = 0; ch < 12; ch++)
                        {
                            if (activeChannels12[ch] && channelZeroingCounts12[ch] < ZeroingTargetPackets)
                            {
                                allActiveDone = false;
                                break;
                            }
                        }

                        if (allActiveDone)
                        {
                            // 计算零点偏移
                            for (int ch = 0; ch < 12; ch++)
                            {
                                if (activeChannels12[ch] &&
                                    pressureCalibBuffers12.ContainsKey(ch) &&
                                    pressureCalibBuffers12[ch].Count > 0)
                                {
                                    channelZeroOffsets12[ch] = pressureCalibBuffers12[ch].Average();
                                }
                            }

                            // 校零完成 - 清理滤波缓冲区，避免新旧基准数据混合影响滤波效果
                            // 重要：校零后零点偏移改变，滤波缓冲区中的旧数据（基于旧偏移）会与新数据（基于新偏移）混合
                            // 导致Hampel Filter的中位数和MAD计算不准确，可能误判异常值
                            channelBuffers12.Clear();           // 实时压力数据滤波缓冲区
                            channelBuffers_filedata12.Clear();  // 文件保存压力数据滤波缓冲区

                            // 校零完成
                            isZeroing12 = false;
                            isSaving12 = true;
                            Array.Clear(channelZeroingCounts12, 0, channelZeroingCounts12.Length);
                            Array.Clear(activeChannels12, 0, activeChannels12.Length);

                            MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    // === 解析值并填充对象 ===
                    for (int i = 0; i < 12; i++)
                    {
                        int channelIndex = i;
                        if (!double.TryParse(uiData[2 + i], out double value)) continue;

                        if (type == "F4") // 温度
                        {
                            //dotUpdate_Temp.TempValues[channelIndex] = Math.Round(value / 1000.0, 1);
                            dotUpdate12.TempValues[channelIndex] = value;
                            /*                            if (wendutu)
                                                        {
                                                            var graphUpdate = new GraphUpdate
                                                            {
                                                                SensorIndex = 12,
                                                                Channel = channelIndex,
                                                                Index = packetIndex12,
                                                                Temperature = dotUpdate12.TempValues[channelIndex]
                                                            };

                                                            if (tempQueue.Count >= MaxQueueSize) tempQueue.TryDequeue(out _);
                                                            tempQueue.Enqueue(graphUpdate);
                                                        }*/
                        }
                        else if (type == "F5") // 压力
                        {
                            double correctedPressure = value - channelZeroOffsets12[channelIndex];
                            double pressureDenoised = HampelFilter(channelIndex, correctedPressure, channelBuffers12);

                            dotUpdate12.PressureValues[channelIndex] = pressureDenoised;

                            if (yalitu)
                            {

                                var graphUpdate = new GraphUpdate
                                {
                                    SensorIndex = 12,
                                    Channel = channelIndex,
                                    Index = packetIndex12,
                                    Pressure = pressureDenoised
                                };

                                if (graphQueue.Count >= MaxQueueSize) graphQueue.TryDequeue(out _);
                                graphQueue.Enqueue(graphUpdate);
                            }
                        }
                    }

                    // === 入队 UI 显示前检查是否全 0 ===
                    bool HasNonZero(double[] arr)
                    {
                        foreach (var v in arr)
                            if (v != 0) return true;
                        return false;
                    }

                    if (diantu)
                    {
                        // 修复：入队时创建对象的深拷贝，避免后续处理修改已入队的数据
                        if (HasNonZero(dotUpdate12.TempValues))
                        {
                            if (dotQueue_Temp.Count >= MaxQueueSize) dotQueue_Temp.TryDequeue(out _);
                            dotQueue_Temp.Enqueue(dotUpdate12.Clone());
                        }
                        if (HasNonZero(dotUpdate12.PressureValues))
                        {
                            if (dotQueue_Pres.Count >= MaxQueueSize) dotQueue_Pres.TryDequeue(out _);
                            dotQueue_Pres.Enqueue(dotUpdate12.Clone());
                        }
                    }

                    Interlocked.Increment(ref packetIndex12);
                }
                else if (uiData[0] == "S32")
                {
                    dotUpdate32.SensorIndex = 32;
                    dotUpdate32.PressureValues = new double[32];
                    dotUpdate32.TempValues = new double[32];
                    string type = uiData[1];

                    if (isZeroing32 && type == "F5")
                    {
                        for (int i = 0; i < 32; i++)
                        {
                            int channelIndex = i;
                            if (channelIndex < 0 || channelIndex >= 32) continue;

                            if (!pressureCalibBuffers32.ContainsKey(channelIndex))
                                pressureCalibBuffers32[channelIndex] = new List<double>();

                            if (double.TryParse(uiData[2 + i], out double pressure))
                            {
                                pressureCalibBuffers32[channelIndex].Add(pressure);
                                channelZeroingCounts32[channelIndex]++;
                                activeChannels32[channelIndex] = true;
                            }
                        }

                        // 判断已激活的通道是否都采满
                        bool allActiveDone = true;
                        for (int ch = 0; ch < 32; ch++)
                        {
                            if (activeChannels32[ch] && channelZeroingCounts32[ch] < ZeroingTargetPackets)
                            {
                                allActiveDone = false;
                                break;
                            }
                        }

                        if (allActiveDone)
                        {
                            // 计算零点偏移
                            for (int ch = 0; ch < 32; ch++)
                            {
                                if (activeChannels32[ch] &&
                                    pressureCalibBuffers32.ContainsKey(ch) &&
                                    pressureCalibBuffers32[ch].Count > 0)
                                {
                                    channelZeroOffsets32[ch] = pressureCalibBuffers32[ch].Average();
                                }
                            }

                            // 校零完成 - 清理滤波缓冲区，避免新旧基准数据混合影响滤波效果
                            // 重要：校零后零点偏移改变，滤波缓冲区中的旧数据（基于旧偏移）会与新数据（基于新偏移）混合
                            // 导致Hampel Filter的中位数和MAD计算不准确，可能误判异常值
                            channelBuffers32.Clear();           // 实时压力数据滤波缓冲区
                            channelBuffers_filedata32.Clear();  // 文件保存压力数据滤波缓冲区

                            // 校零完成
                            isZeroing32 = false;
                            isSaving32 = true;
                            Array.Clear(channelZeroingCounts32, 0, channelZeroingCounts32.Length);
                            Array.Clear(activeChannels32, 0, activeChannels32.Length);

                            Action showMsg = () => MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    // === 解析值并填充对象 ===
                    for (int i = 0; i < 32; i++)
                    {
                        int channelIndex = i;
                        if (!double.TryParse(uiData[2 + i], out double value)) continue;

                        if (type == "F4") // 温度
                        {
                            //dotUpdate_Temp.TempValues[channelIndex] = Math.Round(value / 1000.0, 1);
                            dotUpdate32.TempValues[channelIndex] = value;
                            /*                            if (wendutu)
                                                        {
                                                            var graphUpdate = new GraphUpdate
                                                            {
                                                                SensorIndex = 32,
                                                                Channel = channelIndex,
                                                                Index = packetIndex32,
                                                                Temperature = dotUpdate32.TempValues[channelIndex]
                                                            };

                                                            if (tempQueue.Count >= MaxQueueSize) tempQueue.TryDequeue(out _);
                                                            tempQueue.Enqueue(graphUpdate);
                                                        }*/
                        }
                        else if (type == "F5") // 压力
                        {
                            double correctedPressure = value - channelZeroOffsets32[channelIndex];
                            double pressureDenoised = HampelFilter(channelIndex, correctedPressure, channelBuffers32);

                            dotUpdate32.PressureValues[channelIndex] = pressureDenoised;

                            if (yalitu)
                            {

                                var graphUpdate = new GraphUpdate
                                {
                                    SensorIndex = 32,
                                    Channel = channelIndex,
                                    Index = packetIndex32,
                                    Pressure = pressureDenoised
                                };

                                if (graphQueue.Count >= MaxQueueSize) graphQueue.TryDequeue(out _);
                                graphQueue.Enqueue(graphUpdate);
                            }
                        }
                    }

                    // === 入队 UI 显示前检查是否全 0 ===
                    bool HasNonZero(double[] arr)
                    {
                        foreach (var v in arr)
                            if (v != 0) return true;
                        return false;
                    }

                    if (diantu)
                    {
                        // 修复：入队时创建对象的深拷贝，避免后续处理修改已入队的数据
                        if (HasNonZero(dotUpdate32.TempValues))
                        {
                            if (dotQueue_Temp.Count >= MaxQueueSize) dotQueue_Temp.TryDequeue(out _);
                            dotQueue_Temp.Enqueue(dotUpdate32.Clone());
                        }
                        if (HasNonZero(dotUpdate32.PressureValues))
                        {
                            if (dotQueue_Pres.Count >= MaxQueueSize) dotQueue_Pres.TryDequeue(out _);
                            dotQueue_Pres.Enqueue(dotUpdate32.Clone());
                        }
                    }

                    Interlocked.Increment(ref packetIndex32);
                }

            }
            catch (Exception ex)
            {
                SafeLogger.LogException("ProcessPacketForUI error", ex);
            }
        }

        #endregion

        #region 绘制线程
        private bool _autoScrollX = true;
        private const int Sensor12ChannelCount = 12;
        private const int Sensor32ChannelCount = 32;
        // 统一设置绘制点数的窗口大小
        private const int PlotWindowSize = 100;
        // 使用 1000 作为 Key 的乘数，确保 SensorIndex 不会冲突（12000 vs 32000）
        private const int KeyMultiplier = 1000;

        private Dictionary<int, (CircularBuffer Buffer, List<double> Xs, List<double> Ys, ScottPlot.Plottables.Scatter Plotable)> _dataDictionary = new();
        private readonly System.Windows.Forms.Timer _plotTimer = new();

        private void InitializePlots()
        {
            // --- formsPlot1 (Sensor 12) ---
            var plot1 = formsPlot1.Plot;
            plot1.Clear();
            plot1.Title("Sensor12");
            plot1.Axes.Bottom.Label.Text = "Packet Index";
            plot1.Axes.Left.Label.Text = "Pressure";
            plot1.Legend.IsVisible = true;
            plot1.Legend.Location = ScottPlot.Alignment.LowerRight;

            // 初始化每个通道的数据结构 (Sensor 12 有 12 个通道)
            for (int ch = 0; ch < 12; ch++)
            {
                int key = 12 * KeyMultiplier + ch;
                var buffer = new CircularBuffer(PlotWindowSize);
                var xs = new List<double>(PlotWindowSize);
                var ys = new List<double>(PlotWindowSize);
                var plotable = formsPlot1.Plot.Add.Scatter(xs, ys);
                plotable.Label = $"CH{ch+1}";
                // 平滑刷新优化：启用平滑线条模式，减少视觉跳跃
                plotable.LineStyle.Width = 1.5f;
                plotable.MarkerStyle.Size = 0; // 隐藏标记点，减少绘制开销
                _dataDictionary.Add(key, (buffer, xs, ys, plotable));
            }

            // --- formsPlot2 (Sensor 32) ---
            var plot2 = formsPlot2.Plot;
            plot2.Clear();
            plot2.Title("Sensor32");
            plot2.Axes.Bottom.Label.Text = "Packet Index";
            plot2.Axes.Left.Label.Text = "Pressure";
            plot2.Legend.IsVisible = true;
            plot2.Legend.Location = ScottPlot.Alignment.LowerRight;

            // 初始化每个通道的数据结构 (Sensor 32 假设有 32 个通道)
            for (int ch = 0; ch < 32; ch++)
            {
                int key = 32 * KeyMultiplier + ch;
                var buffer = new CircularBuffer(PlotWindowSize);
                var xs = new List<double>(PlotWindowSize);
                var ys = new List<double>(PlotWindowSize);
                var plotable = formsPlot2.Plot.Add.Scatter(xs, ys);
                plotable.Label = $"CH{ch+1}";
                // 平滑刷新优化：启用平滑线条模式，减少视觉跳跃
                plotable.LineStyle.Width = 1.5f;
                plotable.MarkerStyle.Size = 0; // 隐藏标记点，减少绘制开销
                _dataDictionary.Add(key, (buffer, xs, ys, plotable));
            }

            
            // 首次渲染
            HookUserInteraction(formsPlot1);
            HookUserInteraction(formsPlot2);
            formsPlot1.Refresh();
            formsPlot2.Refresh();
        }
        private void HookUserInteraction(ScottPlot.WinForms.FormsPlot fp)
        {
            fp.MouseWheel += (s, e) =>
            {
                _autoScrollX = false;
            };

            fp.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    _autoScrollX = false;
            };
        }

        private void InitializePlotTimer()
        {
            // 平滑刷新优化：提高刷新频率，使用30ms间隔（约33Hz），保证流畅度
            // 33Hz已经超过人眼24fps的流畅度要求，同时不会造成过大的CPU负担
            _plotTimer.Interval = 30;
            _plotTimer.Tick += PlotTimer_Tick;
            _plotTimer.Start();
        }
        
        // 工业级：图表刷新性能监控
        private DateTime lastPlotRefreshTime = DateTime.Now;
        private int plotRefreshCount = 0;

        private void PlotTimer_Tick(object? sender, EventArgs e)
        {
/*            doubleBufferedPanelCloud12.Values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
            doubleBufferedPanelCloud12.Invalidate();
            doubleBufferedPanelCloud32.Values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32];
            doubleBufferedPanelCloud32.Invalidate();*/
            // 用于记录哪些图表需要刷新
            bool refreshPlot1 = false;
            bool refreshPlot2 = false;

            // 跟踪哪些通道有更新，只更新这些通道
            HashSet<int> updatedChannels12 = new HashSet<int>();
            HashSet<int> updatedChannels32 = new HashSet<int>();
            double maxX12 = 0;
            double maxX32 = 0;

            // 批量提取队列中的所有数据
            while (graphQueue.TryDequeue(out var update))
            {
                int key = update.SensorIndex * KeyMultiplier + update.Channel;

                if (_dataDictionary.TryGetValue(key, out var data))
                {
                    // 1. 追加数据到缓冲区
                    data.Buffer.Add(update.Index, update.Pressure);

                    // 2. 记录更新的通道和最大X值
                    if (update.SensorIndex == 12)
                    {
                        refreshPlot1 = true;
                        updatedChannels12.Add(update.Channel);
                        if (update.Index > maxX12) maxX12 = update.Index;
                    }
                    else if (update.SensorIndex == 32)
                    {
                        refreshPlot2 = true;
                        updatedChannels32.Add(update.Channel);
                        if (update.Index > maxX32) maxX32 = update.Index;
                    }
                }
            }

            // 平滑刷新优化：移除交错刷新，改为同时刷新但优化性能
            // 使用增量更新和双缓冲技术保证流畅度
            if (refreshPlot1)
            {
                var plt1 = formsPlot1.Plot;

                // 只更新有变化的通道，而不是所有通道
                foreach (int channel in updatedChannels12)
                {
                    int key = 12 * KeyMultiplier + channel;
                    if (_dataDictionary.TryGetValue(key, out var data))
                    {
                        data.Buffer.CopyTo(data.Xs, data.Ys);
                    }
                }

                // 优化：直接计算最大X值，避免多次LINQ查询
                if (updatedChannels12.Count > 0)
                {
                    // 如果已从更新中获取了maxX12，直接使用；否则遍历查找
                    if (maxX12 == 0)
                    {
                        foreach (int channel in updatedChannels12)
                        {
                            int key = 12 * KeyMultiplier + channel;
                            if (_dataDictionary.TryGetValue(key, out var data) && data.Xs.Count > 0)
                            {
                                double lastX = data.Xs[data.Xs.Count - 1];
                                if (lastX > maxX12) maxX12 = lastX;
                            }
                        }
                    }

                    if (_autoScrollX && maxX12 > 0)
                    {
                        // 平滑刷新：使用平滑的X轴滚动，避免跳跃
                        double currentMin = plt1.Axes.Bottom.Min;
                        double currentMax = plt1.Axes.Bottom.Max;
                        double targetMin = Math.Max(maxX12 - PlotWindowSize, 0);
                        double targetMax = maxX12;
                        
                        // 如果变化不大，使用平滑过渡；否则直接设置
                        if (Math.Abs(currentMax - targetMax) > PlotWindowSize * 0.1)
                        {
                            plt1.Axes.SetLimitsX(targetMin, targetMax);
                        }
                        else
                        {
                            // 平滑过渡：逐步移动窗口
                            double newMin = Math.Max(targetMin, currentMin + (targetMin - currentMin) * 0.3);
                            double newMax = currentMax + (targetMax - currentMax) * 0.3;
                            plt1.Axes.SetLimitsX(newMin, newMax);
                        }
                        plt1.Axes.AutoScaleY();
                    }
                }

                // 平滑刷新：使用Invalidate触发异步重绘，更平滑
                formsPlot1.Invalidate();
            }

            if (refreshPlot2)
            {
                var plt2 = formsPlot2.Plot;

                // 只更新有变化的通道
                foreach (int channel in updatedChannels32)
                {
                    int key = 32 * KeyMultiplier + channel;
                    if (_dataDictionary.TryGetValue(key, out var data))
                    {
                        data.Buffer.CopyTo(data.Xs, data.Ys);
                    }
                }

                // 优化：直接计算最大X值
                if (updatedChannels32.Count > 0)
                {
                    if (maxX32 == 0)
                    {
                        foreach (int channel in updatedChannels32)
                        {
                            int key = 32 * KeyMultiplier + channel;
                            if (_dataDictionary.TryGetValue(key, out var data) && data.Xs.Count > 0)
                            {
                                double lastX = data.Xs[data.Xs.Count - 1];
                                if (lastX > maxX32) maxX32 = lastX;
                            }
                        }
                    }

                    if (_autoScrollX && maxX32 > 0)
                    {
                        // 平滑刷新：使用平滑的X轴滚动，避免跳跃
                        double currentMin = plt2.Axes.Bottom.Min;
                        double currentMax = plt2.Axes.Bottom.Max;
                        double targetMin = Math.Max(maxX32 - PlotWindowSize, 0);
                        double targetMax = maxX32;
                        
                        // 如果变化不大，使用平滑过渡；否则直接设置
                        if (Math.Abs(currentMax - targetMax) > PlotWindowSize * 0.1)
                        {
                            plt2.Axes.SetLimitsX(targetMin, targetMax);
                        }
                        else
                        {
                            // 平滑过渡：逐步移动窗口
                            double newMin = Math.Max(targetMin, currentMin + (targetMin - currentMin) * 0.3);
                            double newMax = currentMax + (targetMax - currentMax) * 0.3;
                            plt2.Axes.SetLimitsX(newMin, newMax);
                        }
                        plt2.Axes.AutoScaleY();
                    }
                }

                // 平滑刷新：使用Refresh()方法刷新图表
                formsPlot2.Refresh();
            }

            // 处理点阵图数据
            while (dotQueue_Pres.TryDequeue(out var update))
            {
                if (update.SensorIndex == 12)
                {
                    doubleBufferedPanelCloud12.Values = update.PressureValues;
                    doubleBufferedPanelCloud12.Invalidate();
                }
                else if (update.SensorIndex == 32)
                {
                    doubleBufferedPanelCloud32.Values = update.PressureValues;
                    doubleBufferedPanelCloud32.Invalidate();
                }
            }
            while (dotQueue_Temp.TryDequeue(out var update))
            {
                if (update.SensorIndex == 12)
                {
                    doubleBufferedPanelCloud12.ValuesTemp = update.TempValues;
                    doubleBufferedPanelCloud12.Invalidate();
                }
                else if (update.SensorIndex == 32)
                {
                    doubleBufferedPanelCloud32.ValuesTemp = update.TempValues;
                    doubleBufferedPanelCloud32.Invalidate();
                }
            }
        }

        /// <summary>
        /// 根据用户选择的通道重新绘制图表。
        /// </summary>
        private void RedrawSelectedChannels(FormsPlot formsPlot, List<string> selectedChannels, int sensorIndex)
        {
            var plot = formsPlot.Plot;

            // 1. 清空当前图表上的所有内容（旧曲线、标题、轴标签等会保留）
            // 为了更彻底的初始化，我们可以使用 Clear()，但需要重新设置标题和轴。
            // 如果只想清除曲线，可以使用 RemoveAll();
            plot.Clear();
            // 重新设置 ScottPlot 5 轴标签和标题（防止 Clear() 清除）
            plot.Title($"Sensor{sensorIndex}");
            plot.Axes.Bottom.Label.Text = "Packet Index";
            plot.Axes.Left.Label.Text = "Pressure";
            plot.Legend.IsVisible = true;
            plot.Legend.Location = ScottPlot.Alignment.LowerRight;

            // 2. 遍历选中的通道并重新添加绘图对象
            foreach (var selectedText in selectedChannels)
            {
                // 假设 selectedText 格式为 "CH0", "CH1", ...
                if (selectedText.StartsWith("CH") && int.TryParse(selectedText.Substring(2), out int channelIndex))
                {
                    channelIndex = channelIndex - 1;
                    // 构建字典 Key
                    int key = sensorIndex * KeyMultiplier + channelIndex;

                    if (_dataDictionary.TryGetValue(key, out var data))
                    {
                        // 3. 将已有的数据缓冲区添加到 ScottPlot 控件上
                        // ScottPlot 5 的 Scatter 绘图对象可以直接引用 List<double>
                        var newPlotable = plot.Add.Scatter(data.Xs, data.Ys);
                        newPlotable.Label = $"CH{channelIndex}";

                        // 4. 更新字典中的 Plotable 引用
                        // 这一步非常重要！因为 Scatter 对象是新的，必须更新字典中对它的引用，
                        // 否则 PlotTimer_Tick 将无法找到并更新它。
                        _dataDictionary[key] = (data.Buffer, data.Xs, data.Ys, newPlotable);
                    }
                }
            }

            // 5. 自动缩放和刷新
            // 首次绘制或重绘时，让图表自动适应所有绘制的数据范围。
            plot.Axes.AutoScale();
            formsPlot.Refresh();

        }
        public class CircularBuffer
        {
            private readonly double[] _xs;
            private readonly double[] _ys;
            private int _writeIndex = 0;
            private int _count = 0;

            public int Capacity { get; }
            public int Count => _count;

            public CircularBuffer(int capacity)
            {
                Capacity = capacity;
                _xs = new double[capacity];
                _ys = new double[capacity];
            }

            public void Add(double x, double y)
            {
                _xs[_writeIndex] = x;
                _ys[_writeIndex] = y;

                _writeIndex = (_writeIndex + 1) % Capacity;
                if (_count < Capacity)
                    _count++;
            }

            public void CopyTo(List<double> xs, List<double> ys)
            {
                if (_count == 0)
                {
                    xs.Clear();
                    ys.Clear();
                    return;
                }

                // 优化：预分配容量，减少内存重新分配
                if (xs.Capacity < _count)
                {
                    xs.Capacity = _count;
                }
                if (ys.Capacity < _count)
                {
                    ys.Capacity = _count;
                }

                xs.Clear();
                ys.Clear();

                int start = (_writeIndex - _count + Capacity) % Capacity;

                // 优化：直接添加，避免多次扩容
                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % Capacity;
                    xs.Add(_xs[idx]);
                    ys.Add(_ys[idx]);
                }
            }
        }

        #endregion

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                yalitu = true;
            }
            else
            {
                yalitu = false;
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                diantu = true;
            }
            else
            {
                diantu = false;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            isZeroing12 = true;
            isZeroing32 = true;
            pressureCalibBuffers12.Clear();
            pressureCalibBuffers32.Clear();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            _autoScrollX = true;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            _autoScrollX = true;
        }

    }
}

