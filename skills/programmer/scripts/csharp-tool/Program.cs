/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Program.cs
수정일 : 2026-09-17

# 설명
입력 경로 집합을 파일별 분석 파이프라인과 출력기에 연결하는 실행 진입점이다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// C# 분석 도구의 명령줄 실행 흐름을 조정한다.
    /// </summary>
    // ============================================================
    internal static class Program
    {

    #region 실행 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 인수를 해석하고 선택된 모든 C# 파일을 한 프로세스에서 처리한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int Main(string[] args)
        {
            // 파일 시스템 해석부터 출력까지 한 예외 경계 안에 둬 모든 입력 형태가 같은 종료 계약을 사용하게 한다.
            try
            {
                return Run(args);
            }
            catch (IOException exception)
            {
                return WriteAnalysisError(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return WriteAnalysisError(exception.Message);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("TOOL ERROR");
                Console.Error.WriteLine(exception.ToString());
                return (int)ExitCode.ToolFailure;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 명령 해석부터 배치 분석과 출력까지 정상 실행 경로를 수행한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int Run(string[] args)
        {
            // CLI 문법과 실제 입력 경로 해석을 분리해 여러 파일·디렉터리를 같은 처리 단위로 만든다.
            if (!CommandLineRequest.TryParse(args, out CommandLineRequest request, out string error))
            {
                Console.Error.WriteLine(error);
                return (int)ExitCode.InvalidArgument;
            }

            if (!InputPathResolver.TryResolve(request.InputPaths, out List<string> filePaths, out error))
            {
                Console.Error.WriteLine($"TOOL ERROR {error}");
                return (int)ExitCode.AnalysisError;
            }

            Stopwatch totalWatch = Stopwatch.StartNew();
            double analysisMilliseconds = 0.0;
            double ruleMilliseconds = 0.0;

            // 모든 파일이 같은 프로세스와 Roslyn 로딩 비용을 공유하도록 결과를 한 번에 수집한다.
            List<FileReport> reports = new();
            foreach (string filePath in filePaths)
            {
                FileReport report = AnalyzeFile(filePath, request.Command, out double analysisElapsed, out double ruleElapsed);
                reports.Add(report);
                analysisMilliseconds += analysisElapsed;
                ruleMilliseconds += ruleElapsed;
            }

            // 파일별 결과가 모두 준비된 뒤 한 번만 렌더링해 text와 JSON이 같은 배치 계약을 공유하게 한다.
            Stopwatch renderWatch = Stopwatch.StartNew();
            string output = request.Format == "json"
                ? JSONReportWriter.Write(reports, request.Command)
                : TextReportWriter.Write(reports, request.Command);
            renderWatch.Stop();
            Console.WriteLine(output);

            totalWatch.Stop();
            if (request.Timing)
            {
                WriteTiming(filePaths.Count, analysisMilliseconds, ruleMilliseconds, renderWatch.Elapsed.TotalMilliseconds, totalWatch.Elapsed.TotalMilliseconds);
            }

            return GetExitCode(reports);
        }

    #endregion

    #region 파일 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 하나를 분석하고 필요할 때 컨벤션 진단까지 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static FileReport AnalyzeFile(string filePath, string command, out double analysisMilliseconds, out double ruleMilliseconds)
        {
            // 구조 분석은 inspect와 check/review가 공유하므로 파일마다 정확히 한 번만 수행한다.
            Stopwatch analysisWatch = Stopwatch.StartNew();
            CSharpFileAnalysis analysis = CSharpFileAnalyzer.Analyze(filePath);
            analysisWatch.Stop();
            analysisMilliseconds = analysisWatch.Elapsed.TotalMilliseconds;

            // inspect는 구조만 필요하고, 파싱 오류가 있는 파일은 AST 기반 컨벤션 판정을 신뢰할 수 없으므로 규칙 계산을 생략한다.
            Stopwatch ruleWatch = Stopwatch.StartNew();
            bool canCheckConventions = command != "inspect" && analysis.ParseDiagnostics.Count == 0;
            List<ConventionDiagnostic> diagnostics = canCheckConventions
                ? ConventionChecker.Check(analysis)
                : new();
            ruleWatch.Stop();
            ruleMilliseconds = ruleWatch.Elapsed.TotalMilliseconds;

            return new()
            {
                Analysis = analysis,
                Diagnostics = diagnostics,
            };
        }

    #endregion

    #region 출력과 종료

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 전체 결과에서 가장 높은 우선순위의 종료 코드를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetExitCode(IReadOnlyList<FileReport> reports)
        {
            // 파싱 오류가 하나라도 있으면 컨벤션 결과보다 우선해 분석 실패로 반환한다.
            if (reports.Any(report => report.Analysis.ParseDiagnostics.Count > 0))
            {
                return (int)ExitCode.AnalysisError;
            }

            if (reports.Any(report => report.Diagnostics.Any(diagnostic => diagnostic.Severity == ConventionSeverity.Error)))
            {
                return (int)ExitCode.ConventionError;
            }

            return (int)ExitCode.Success;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 분석 단계의 예외 메시지를 공통 도구 오류 형식으로 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int WriteAnalysisError(string message)
        {
            Console.Error.WriteLine("TOOL ERROR");
            Console.Error.WriteLine($"  {message}");
            return (int)ExitCode.AnalysisError;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 파일 처리의 누적 시간과 파일당 평균 시간을 한 줄로 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteTiming(int fileCount, double analysisMilliseconds, double ruleMilliseconds, double renderMilliseconds, double totalMilliseconds)
        {
            double averageMilliseconds = totalMilliseconds / fileCount;
            string timingLine = $"TIMING files={fileCount} analysis={analysisMilliseconds:F2}ms rules={ruleMilliseconds:F2}ms " +
                $"render={renderMilliseconds:F2}ms total={totalMilliseconds:F2}ms avg={averageMilliseconds:F2}ms/file";
            Console.Error.WriteLine(timingLine);
        }

    #endregion

    }

}
