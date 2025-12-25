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
        private StreamWriter packetWriter;
        private Thread serialThread;
        private Thread serialSendThread;
        private CancellationTokenSource cts;
        private List<string> biaoTouName = new List<string> { "LogTime", "Sensor", };
        byte[][] memsCommands = new byte[2][];
        private void button_open_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                if (foreverComboBox_port.SelectedIndex == -1)
                {
                    MessageBox.Show("请选择串口");
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

                packetWriter?.Flush();
                packetWriter?.Close();
                packetWriter = null;

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
                memsCommands = new byte[2][];
                List<int> fingerNum = [1,2];
                for (int i = 0; i < fingerNum.Count; i++)
                {
                    memsCommands[i] = new byte[] { 0x7B, 0xB7, (byte)(fingerNum[i]) };
                }

                // 启动后台读取线程
                cts = new CancellationTokenSource();
                serialThread = new Thread(() => SerialReadLoop(cts.Token));
                serialThread.IsBackground = true;
                serialThread.Start();

                serialSendThread = new Thread(() => SerialSendLoop(cts.Token, 2));
                serialSendThread.IsBackground = true;
                serialSendThread.Start();

                savePath = crownTextBox_save.Text;
                // 生成文件路径
                string fileSavePath = System.IO.Path.Combine(savePath,
                    $"MEMS_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                // 创建全局 StreamWriter，不写表头
                packetWriter = new StreamWriter(fileSavePath, true, new System.Text.UTF8Encoding(false));

                packetWriter.WriteLine(string.Join(",", biaoTouName));

                packetWriter.AutoFlush = true;

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
        private readonly BlockingCollection<string> fileQueue = new BlockingCollection<string>(new ConcurrentQueue<string>(), 20000);
        private readonly BlockingCollection<List<string>> fileRawQueue = new BlockingCollection<List<string>>(new ConcurrentQueue<List<string>>(), 20000);
        private Thread fileWriterThread;
        private Thread formatThread;
        private object saveLock = new object();
        private long totalPacketCount = 0;
        private long savedPacketCount = 0;

        private void StartWorkers()
        {
            // 启动格式化工人线程
            formatThread = new Thread(FormatWorkerLoop) { IsBackground = true, Name = "FormatWorker" };
            formatThread.Start();

            // 你原来的 fileWriterThread 维持不变
            fileWriterThread = new Thread(FileWriterLoop) { IsBackground = true, Name = "FileWriter" };
            fileWriterThread.Start();
        }
        private void FileWriterLoop()
        {
            try
            {
                while (!fileQueue.IsCompleted)
                {
                    var batch = new List<string>();
                    while (fileQueue.TryTake(out var line))
                        batch.Add(line);

                    if (batch.Count > 0)
                    {
                        lock (saveLock)
                        {
                            foreach (var line in batch)
                            {
                                packetWriter.WriteLine(line);
                                Interlocked.Increment(ref savedPacketCount);
                            }
                            packetWriter.Flush();
                        }

                        // UI 更新（保持你的逻辑）
                        if (label_save.InvokeRequired)
                            label_save.BeginInvoke(new Action(() =>
                                label_save.Text = $"已存包数: {savedPacketCount}"));
                        else
                            label_save.Text = $"已存包数: {savedPacketCount}";
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                // 工业级：文件写线程异常时记录但不弹窗（避免阻塞）
                SafeLogger.LogException("文件写线程异常", ex);
            }
        }
        private void FormatWorkerLoop()
        {
            try
            {
                foreach (var packet in fileRawQueue.GetConsumingEnumerable())
                {
                    string line = "";
                    line = FormatPacketToOneCsvLineFast(packet);

                    if (line == null) continue;

                    // fileQueue 有界 + 丢最旧，确保不堆积
                    if (fileQueue.Count >= 20000) fileQueue.TryTake(out _);
                    fileQueue.Add(line);
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

        private void SerialSendLoop(CancellationToken token, int count)
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
        }
        private void SerialReadLoop(CancellationToken token)
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
                if (packet.Length < 10) return;

                int length = packet[2];
                byte addr = packet[3];
                byte type = packet[4];
                if (addr != 1 && addr != 2) return;

                if (addr == 1)
                {
                    // 复用数组
                    Span<double> values = stackalloc double[12];

                    int dataOffset = 13;

                    // 如果是温度数据 (F4)
                    if (type == 0xF4)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            if (dataOffset + i * 2 + 1 >= packet.Length) break;
                            double v = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(dataOffset + i * 2, 2));
                            values[11 - i] = v / 10;
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
                        for (int i = 0; i < 12; i++)
                        {
                            if (dataOffset + i * 4 + 3 >= packet.Length) break;
                            double v = BitConverter.ToInt32(packet, dataOffset + i * 4);
                            if (guiyihua)
                            {
                                v = v / 1000.0; // Kpa

                                // 保证最小值为 1
                                if (v > 0 && v < 1) v = 1;
                                if (v < 0 && v > -1) v = -1;
                            }

                            values[11 - i] = v;
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
                            double filedV = DenoiseByMedian_temp12(channelIndex, rawV);
                            fileData.Add(filedV.ToString("F2"));
                        }
                        for (int i = 0; i < 12; i++)
                        {
                            double rawV = finalData12.PressureData[i];
                            int channelIndex = i;
                            double zeroedV = rawV - channelZeroOffsets12[channelIndex];
                            double filedV = DenoiseByMedian_filedata12(channelIndex, zeroedV);
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
                        for (int i = 0; i < 32; i++)
                        {
                            if (dataOffset + i * 2 + 1 >= packet.Length) break;
                            double v = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(dataOffset + i * 2, 2));
                            values[31 - i] = v / 10;
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
                        for (int i = 0; i < 32; i++)
                        {
                            if (dataOffset + i * 4 + 3 >= packet.Length) break;
                            double v = BitConverter.ToInt32(packet, dataOffset + i * 4);
                            if (guiyihua)
                            {
                                v = v / 1000.0; // Kpa

                                // 保证最小值为 1
                                if (v > 0 && v < 1) v = 1;
                                if (v < 0 && v > -1) v = -1;
                            }

                            values[31 - i] = v;
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
                            double filedV = DenoiseByMedian_temp32(channelIndex, rawV);
                            fileData.Add(filedV.ToString("F2"));
                        }
                        for (int i = 0; i < 32; i++)
                        {
                            double rawV = finalData32.PressureData[i];
                            int channelIndex = i;
                            double zeroedV = rawV - channelZeroOffsets32[channelIndex];
                            double filedV = DenoiseByMedian_filedata32(channelIndex, zeroedV);
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

        private double DenoiseByMedian_temp12(int channelIndex, double newValue)
        {
            if (!channelBuffers_temp12.ContainsKey(channelIndex))
                channelBuffers_temp12[channelIndex] = new Queue<double>();

            var buffer = channelBuffers_temp12[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
        }
        private double DenoiseByMedian_filedata12(int channelIndex, double newValue)
        {
            if (!channelBuffers_filedata12.ContainsKey(channelIndex))
                channelBuffers_filedata12[channelIndex] = new Queue<double>();

            var buffer = channelBuffers_filedata12[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
        }
        private double DenoiseByMedian_temp32(int channelIndex, double newValue)
        {
            if (!channelBuffers_temp32.ContainsKey(channelIndex))
                channelBuffers_temp32[channelIndex] = new Queue<double>();

            var buffer = channelBuffers_temp32[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
        }
        private double DenoiseByMedian_filedata32(int channelIndex, double newValue)
        {
            if (!channelBuffers_filedata32.ContainsKey(channelIndex))
                channelBuffers_filedata32[channelIndex] = new Queue<double>();

            var buffer = channelBuffers_filedata32[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
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

                            // 校零完成
                            isZeroing12 = false;
                            isSaving12 = true;
                            Array.Clear(channelZeroingCounts12, 0, channelZeroingCounts12.Length);
                            Array.Clear(activeChannels12, 0, activeChannels12.Length);

                            Action showMsg = () => MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                            double pressureDenoised = DenoiseByMedian12(channelIndex, correctedPressure);

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
                            double pressureDenoised = DenoiseByMedian32(channelIndex, correctedPressure);

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
        private double DenoiseByMedian12(int channelIndex, double newValue)
        {
            if (!channelBuffers12.ContainsKey(channelIndex))
                channelBuffers12[channelIndex] = new Queue<double>();

            var buffer = channelBuffers12[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
        }
        private double DenoiseByMedian32(int channelIndex, double newValue)
        {
            if (!channelBuffers32.ContainsKey(channelIndex))
                channelBuffers32[channelIndex] = new Queue<double>();

            var buffer = channelBuffers32[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
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
                plotable.Label = $"CH{ch}";
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
                plotable.Label = $"CH{ch}";
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
            // 工业级优化：根据图表数量动态调整刷新间隔
            // 单个图表：50ms (20Hz)，双图表：80ms (12.5Hz)，保证流畅度
            _plotTimer.Interval = 50;
            _plotTimer.Tick += PlotTimer_Tick;
            _plotTimer.Start();
        }
        
        // 工业级：图表刷新性能监控
        private DateTime lastPlotRefreshTime = DateTime.Now;
        private int plotRefreshCount = 0;

        private void PlotTimer_Tick(object? sender, EventArgs e)
        {
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

            // 工业级优化：双图表绘制性能优化
            // 如果两个图表都需要刷新，使用交错刷新策略，避免同时刷新造成卡顿
            bool bothNeedRefresh = refreshPlot1 && refreshPlot2;
            bool refreshPlot1ThisCycle = refreshPlot1 && (!bothNeedRefresh || (plotRefreshCount % 2 == 0));
            bool refreshPlot2ThisCycle = refreshPlot2 && (!bothNeedRefresh || (plotRefreshCount % 2 == 1));

            if (refreshPlot1ThisCycle)
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
                        plt1.Axes.SetLimitsX(Math.Max(maxX12 - PlotWindowSize, 0), maxX12);
                        plt1.Axes.AutoScaleY();
                    }
                }

                formsPlot1.Refresh();
            }

            if (refreshPlot2ThisCycle)
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
                        plt2.Axes.SetLimitsX(Math.Max(maxX32 - PlotWindowSize, 0), maxX32);
                        plt2.Axes.AutoScaleY();
                    }
                }

                formsPlot2.Refresh();
            }

            // 更新刷新计数器
            if (bothNeedRefresh)
            {
                plotRefreshCount++;
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
