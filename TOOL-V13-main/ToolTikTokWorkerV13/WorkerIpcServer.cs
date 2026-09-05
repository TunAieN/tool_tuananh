using System.IO.Pipes;
using System.Text;

namespace ToolTikTokV11;

public sealed class WorkerIpcServer : IDisposable
{
    readonly string _pipeName;
    readonly MainForm _form;
    readonly string _dataRoot;
    readonly CancellationTokenSource _cts = new();
    Task? _loop;
    int _createdLogged;
    int _connectedLogged;

    public WorkerIpcServer(string pipeName, MainForm form, string dataRoot)
    {
        _pipeName = pipeName;
        _form = form;
        _dataRoot = dataRoot;
    }

    public void Start()
    {
        if (_loop is not null) return;
        WorkerBootLog.Write(_dataRoot, $"[WORKER_PIPE_CREATE_START] pipeName={_pipeName}");
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                if (Interlocked.Exchange(ref _createdLogged, 1) == 0)
                {
                    WorkerBootLog.Write(_dataRoot, $"[WORKER_PIPE_CREATED] pipeName={_pipeName}");
                    WorkerBootLog.Write(_dataRoot, $"[WORKER_PIPE_WAIT_CONNECTION] pipeName={_pipeName}");
                }
                await pipe.WaitForConnectionAsync(ct);
                if (Interlocked.Exchange(ref _connectedLogged, 1) == 0)
                    WorkerBootLog.Write(_dataRoot, $"[WORKER_PIPE_CONNECTED] pipeName={_pipeName}");
                var connectedPipe = pipe;
                pipe = null;
                _ = HandleConnectionAsync(connectedPipe, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                WorkerBootLog.Write(_dataRoot, $"[WORKER_PIPE_ERROR] pipeName={_pipeName} exception={ex}");
                if (!ct.IsCancellationRequested) await Task.Delay(100, ct);
            }
            finally { pipe?.Dispose(); }
        }
    }

    async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using (pipe)
        {
            try
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
                var command = await reader.ReadLineAsync(ct) ?? "";
                var response = await _form.HandleManagedCommandAsync(command);
                await writer.WriteLineAsync(response);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                WorkerBootLog.Write(_dataRoot, $"[WORKER_PIPE_CONNECTION_ERROR] pipeName={_pipeName} exception={ex}");
            }
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _loop?.Wait(500); } catch { }
        _cts.Dispose();
    }
}
