using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace DTDTrainer;

/// <summary>
/// Kết nối tới plugin trong game qua named pipe. Một luồng nền giữ kết nối, tự nối lại,
/// gửi lệnh theo thứ tự và hỏi trạng thái (GET) mỗi giây. Sự kiện được gọi trên luồng nền.
/// </summary>
sealed class PipeClient : IDisposable
{
    public const string PipeName = "DTDTrainer.Pipe";

    readonly BlockingCollection<string> _queue = new();
    Thread _thread;
    volatile bool _running = true;
    volatile bool _connected;

    public bool Connected => _connected;

    /// <summary>Danh sách lệnh SET cần gửi ngay khi vừa kết nối để đồng bộ trạng thái.</summary>
    public Func<IEnumerable<string>> SyncCommands { get; set; }

    public event Action<bool> ConnectionChanged;
    public event Action<IReadOnlyDictionary<string, string>> StateReceived;
    public event Action<string, string> Replied;

    /// <summary>Gọi sau khi cửa sổ đã có handle, để sự kiện kết nối không bị bỏ lỡ.</summary>
    public void Start()
    {
        if (_thread != null) return;
        _thread = new Thread(Loop) { IsBackground = true, Name = "DTDTrainer-Pipe" };
        _thread.Start();
    }

    public void Send(string command) => _queue.Add(command);

    void Loop()
    {
        while (_running)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                try { pipe.Connect(1000); }
                catch (TimeoutException) { continue; }

                var enc = new UTF8Encoding(false);
                using var reader = new StreamReader(pipe, enc);
                using var writer = new StreamWriter(pipe, enc) { AutoFlush = true, NewLine = "\n" };

                string Request(string line)
                {
                    writer.WriteLine(line);
                    return reader.ReadLine() ?? throw new IOException("pipe closed");
                }

                if (!Request("PING").StartsWith("PONG")) continue;
                SetConnected(true);
                while (_queue.TryTake(out _)) { } // lệnh cũ đã nằm trong SyncCommands
                foreach (var cmd in SyncCommands?.Invoke() ?? Array.Empty<string>()) Request(cmd);

                var nextPoll = DateTime.MinValue;
                while (_running)
                {
                    if (_queue.TryTake(out var cmd, 200))
                        Replied?.Invoke(cmd, Request(cmd));

                    if (DateTime.UtcNow >= nextPoll)
                    {
                        nextPoll = DateTime.UtcNow.AddSeconds(1);
                        StateReceived?.Invoke(ParseState(Request("GET")));
                    }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception) { Thread.Sleep(500); }
            SetConnected(false);
            if (_running) Thread.Sleep(1000);
        }
    }

    void SetConnected(bool value)
    {
        if (_connected == value) return;
        _connected = value;
        ConnectionChanged?.Invoke(value);
    }

    static IReadOnlyDictionary<string, string> ParseState(string reply)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!reply.StartsWith("STATE ")) return map;
        foreach (var part in reply.Substring(6).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) map[part[..eq]] = part[(eq + 1)..];
        }
        return map;
    }

    public void Dispose()
    {
        _running = false;
        _queue.CompleteAdding();
    }
}
