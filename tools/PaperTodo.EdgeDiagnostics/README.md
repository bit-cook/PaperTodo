# Edge 诊断工具源码

此目录保存真实 PaperTodo 进程使用的可选诊断采集器；不是另一套渲染器，也不是正式产品功能。
当前运行时边界以 [ARCHITECTURE](../../doc/ARCHITECTURE.md) 为准。实验数据和历史取舍仍分别归属 `doc/EXPERIMENTS.md`、`doc/DECISIONS.md`。

## 编译边界

- `PaperTodo.csproj` 从默认源码扫描中排除 `tools/**`，仅在 Debug 配置显式编译本目录的五个 `.cs` 文件。普通 Release 不包含这些采集实现。
- `src/EdgeDiagnosticJournal.cs` 只提供 Release 空入口，没有缓冲区、观察器、计时器或文件输出。真实运行链中的 Debug 埋点保留在业务调用位置。
- 两个诊断检查项目直接引用这里的原始源码，不引用 Release 空入口。主程序不引用测试程序集。
- 这里的采集实现按 #256 的 `bd921038d73f3b0d5775a72342811393861a8c42` 原始 Git blob 搬迁，不改事件结构或采集逻辑。

## Windows 实机采集

使用已安装仓库所需 .NET SDK 的 PowerShell，从仓库根目录执行：

```powershell
dotnet build PaperTodo.csproj -c Debug -o out/edge-diagnostics -p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw '诊断构建失败' }
$env:PAPERTODO_EDGE_DIAGNOSTICS = 'memory'
try {
    & .\out\edge-diagnostics\PaperTodo.exe
} finally {
    Remove-Item Env:PAPERTODO_EDGE_DIAGNOSTICS -ErrorAction SilentlyContinue
}
```

正常退出 PaperTodo 后再读取日志。内存采集按已有机制有界保存，正常退出落盘；不通过诊断回调推进动画。更深的 native/Dispatcher 观察仍使用源码中已有的独立开关，默认不额外打开。

## 行为检查

```powershell
dotnet run --project tests/PaperTodo.EdgeDiagnosticJournalChecks -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Journal 检查失败' }
dotnet run --project tests/PaperTodo.EdgeDiagnosticJournalChecks -c Release
if ($LASTEXITCODE -ne 0) { throw 'Journal Release 检查失败' }
dotnet run --project tests/PaperTodo.EdgeLatencyObservationChecks -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Latency observer 检查失败' }
```

Latency 检查需要 Windows/WPF 桌面。上述命令是执行入口，不代表拆分后的构建或实机验收已经通过。
