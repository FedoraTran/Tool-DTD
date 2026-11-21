using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace DTDMod
{
    /// <summary>
    /// Máy chủ named pipe cho trainer .exe. Giao thức theo dòng (UTF-8):
    ///   PING                      -> PONG DTDMod &lt;version&gt;
    ///   SET &lt;Key&gt; &lt;value&gt;         -> OK | ERR ...
    ///   RESET                     -> OK   (tắt mọi cheat, nút "Tắt hết")
    ///   ACTION &lt;Name&gt; [arg]       -> OK [kết quả] | ERR &lt;mã lỗi&gt;   (chạy trên luồng game, chờ tối đa 3 giây)
    ///   GET                       -> STATE key=value;...
    ///   DIAG                      -> DIAG patches=...|hits=...
    /// Luồng này không bao giờ chạm vào object IL2CPP; mọi thao tác với game
    /// được đẩy vào <see cref="MainThreadQueue"/>.
    /// </summary>
    public static class PipeServer
    {
        public const string PipeName = "DTDTrainer.Pipe";

        public static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();

        static Thread _thread;
        static volatile bool _running;
        static int _clients;

        public static void Start()
        {
            if (_thread != null) return;
            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "DTDMod-Pipe" };
            _thread.Start();
        }

        /// <summary>Mỗi kết nối chạy trên luồng riêng để trainer và công cụ chẩn đoán dùng song song.</summary>
        static void AcceptLoop()
        {
            while (_running)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = new NamedPipeServerStream(PipeName, PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.None);
                    server.WaitForConnection();
                    var s = server;
                    new Thread(() => Serve(s)) { IsBackground = true, Name = "DTDMod-PipeClient" }.Start();
                }
                catch (Exception ex)
                {
                    server?.Dispose();
                    Plugin.LogError("Pipe loi: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        static void Serve(NamedPipeServerStream server)
        {
            Interlocked.Increment(ref _clients);
            Plugin.LogInfo("Trainer da ket noi.");
            try
            {
                using (server)
                using (var reader = new StreamReader(server, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    string line;
                    while (_running && (line = reader.ReadLine()) != null)
                    {
                        string reply;
                        try { reply = Handle(line.Trim()); }
                        catch (Exception ex) { reply = "ERR " + ex.Message.Replace('\n', ' '); }
                        writer.WriteLine(reply);
                    }
                }
            }
            catch (IOException) { /* trainer bị đóng đột ngột */ }
            finally
            {
                // Giống Wand/WeMod: không còn trainer nào kết nối thì mọi mod ngừng.
                if (Interlocked.Decrement(ref _clients) == 0)
                {
                    ModState.ResetAll();
                    Plugin.LogInfo("Trainer da ngat ket noi, tat moi cheat.");
                }
            }
        }

        static string Handle(string line)
        {
            if (line.Length == 0) return "ERR empty";
            string[] parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToUpperInvariant();
            switch (cmd)
            {
                case "PING":
                    return "PONG DTDMod " + Plugin.Version;

                case "SET":
                    if (parts.Length < 3) return "ERR usage: SET <Key> <value>";
                    return ModState.TrySet(parts[1], parts[2]) ? "OK" : "ERR unknown key " + parts[1];

                case "RESET": // nút "Tắt hết" của trainer
                    ModState.ResetAll();
                    Plugin.LogInfo("Trainer yeu cau tat moi cheat.");
                    return "OK";

                case "ACTION":
                    if (parts.Length < 2) return "ERR usage: ACTION <Name> [arg]";
                    return QueueAction(parts[1], parts.Length > 2 ? parts[2] : "");

                case "GET":
                    return "STATE saveLoaded=" + (GameSnapshot.SaveLoaded ? 1 : 0)
                         + ";inDive=" + (GameSnapshot.InDive ? 1 : 0)
                         + ";gold=" + GameSnapshot.Gold
                         + ";bei=" + GameSnapshot.Bei
                         + ";chefFlame=" + GameSnapshot.ChefFlame
                         + ";doni=" + GameSnapshot.Doni
                         + ";jungleChefFlame=" + GameSnapshot.JungleChefFlame
                         + ";jungleClock=" + GameSnapshot.JungleClock
                         + ";patchOk=" + Plugin.PatchesOk + ";patchFail=" + Plugin.PatchesFailed;

                case "DIAG":
                    return "DIAG patches=" + Plugin.PatchReport + "|hits=" + ModState.DescribeHits() + "|state=" + ModState.Describe()
                         + "|smooth=" + DTDMod.Patches.RenderSmoothing.Describe();

                default:
                    return "ERR unknown command " + cmd;
            }
        }

        static string QueueAction(string name, string arg)
        {
            string key = name.ToLowerInvariant();
            switch (key)
            {
                case "setgold":
                case "setbei":
                case "setchefflame":
                case "setdoni":
                case "setjunglechefflame":
                    if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
                        return "ERR number";
                    return RunOnGameThread(() => Currency.Set(key, value));

                case "jungleadvancehour":
                    return RunOnGameThread(() => JungleClock.Shift(+1));

                case "junglerewindhour":
                    return RunOnGameThread(() => JungleClock.Shift(-1));

                default:
                    return "ERR unknown action " + name;
            }
        }

        /// <summary>
        /// Chạy trên luồng game rồi trả kết quả cho trainer. Nếu game đang bận (vd. đang tải cảnh) quá 3 giây
        /// thì trả "OK" luôn: lệnh vẫn nằm trong hàng đợi và sẽ chạy ngay khi game rảnh.
        /// </summary>
        static string RunOnGameThread(Func<string> work)
        {
            string result = null;
            var done = new ManualResetEventSlim(false);
            MainThreadQueue.Enqueue(() =>
            {
                try { result = work(); }
                catch (Exception ex) { result = "ERR " + ex.Message.Replace('\n', ' '); }
                finally { done.Set(); }
            });
            return done.Wait(3000) ? result ?? "OK" : "OK";
        }
    }
}
