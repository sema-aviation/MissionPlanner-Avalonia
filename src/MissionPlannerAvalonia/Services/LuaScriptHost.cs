using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MoonSharp.Interpreter;

namespace MissionPlannerAvalonia.Services;

public class LuaScriptHost {
  private CancellationTokenSource? _cts;
  private int _running;

  public event Action<string>? Output;

  public bool IsRunning => Volatile.Read(ref _running) != 0;

  public Task RunFileAsync(string path) {
    var code = File.ReadAllText(path);
    return RunAsync(code);
  }

  public Task RunAsync(string code) {
    if (Interlocked.Exchange(ref _running, 1) != 0) {
      Emit("Script already running.");
      return Task.CompletedTask;
    }

    _cts = new CancellationTokenSource();
    var token = _cts.Token;

    return Task.Run(() => {
      try {
        var script = new Script();
        script.Options.DebugPrint = s => Emit(s);
        RegisterGlobals(script, token);
        var result = script.DoString(code);
        if (result != null && result.Type != DataType.Nil && result.Type != DataType.Void) {
          Emit(result.ToPrintString());
        }
        Emit("Script finished.");
      } catch (OperationCanceledException) {
        Emit("Script aborted.");
      } catch (InterpreterException ex) {
        Emit("Lua error: " + ex.DecoratedMessage);
      } catch (Exception ex) {
        Emit("Error: " + ex.Message);
      } finally {
        Volatile.Write(ref _running, 0);
        _cts?.Dispose();
        _cts = null;
      }
    });
  }

  public void Abort() {
    _cts?.Cancel();
  }

  private void RegisterGlobals(Script script, CancellationToken token) {
    try {
      UserData.RegisterAssembly(typeof(MissionPlanner.MAVLinkInterface).Assembly);
    } catch {
    }
    try {
      UserData.RegisterType<MissionPlanner.MAVLinkInterface>();
    } catch {
    }
    try {
      var comPort = AppState.comPort;
      script.Globals["comPort"] = comPort;
      script.Globals["cs"] = comPort.MAV.cs;
    } catch (Exception ex) {
      Emit("Vehicle binding unavailable: " + ex.Message);
    }
    script.Globals["mp_should_abort"] = (Func<bool>)(() => token.IsCancellationRequested);
    script.Globals["mp_check_abort"] = (Action)(() => {
      if (token.IsCancellationRequested) {
        throw new OperationCanceledException(token);
      }
    });
  }

  private void Emit(string text) {
    Output?.Invoke(text);
  }
}
