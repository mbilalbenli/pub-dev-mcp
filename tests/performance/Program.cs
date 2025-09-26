using BenchmarkDotNet.Running;
using PubDevMcp.Tests.Performance;

var summary = BenchmarkRunner.Run<McpPerformanceBenchmarks>();
PerformanceBudget.Assert(summary, TimeSpan.FromSeconds(7));
