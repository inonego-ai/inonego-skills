/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : CommandLineRequest.cs
수정일 : 2026-09-17

# 설명
inspect, check, review 명령과 공통 옵션을 해석해 실행 요청을 구성한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 명령줄에서 해석한 실행 요청을 보관한다.
    /// </summary>
    // ============================================================
    internal sealed class CommandLineRequest
    {
        public string Command          = string.Empty;
        public List<string> InputPaths = new();
        public string Format           = "text";
        public bool Timing             = false;

        // ------------------------------------------------------------
        /// <summary>
        /// 인수를 검사해 지원되는 실행 요청으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool TryParse(string[] args, out CommandLineRequest request, out string error)
        {
            request = new();
            error = string.Empty;
            if (args.Length < 2)
            {
                error = "사용법: csharp-tool <inspect|check|review> <path> [<path> ...] [--format text|json] [--timing]";
                return false;
            }

            // 명령을 먼저 확정한 뒤 나머지 인수는 입력 경로와 공통 옵션으로 구분한다.
            string command = args[0].ToLowerInvariant();
            if (command != "inspect" && command != "check" && command != "review")
            {
                error = $"지원하지 않는 명령입니다: {args[0]}";
                return false;
            }

            request.Command = command;
            for (int index = 1; index < args.Length; index++)
            {
                string argument = args[index];
                if (argument == "--timing")
                {
                    request.Timing = true;
                    continue;
                }

                if (argument == "--format")
                {
                    if (!TryReadFormat(args, ref index, request, out error))
                    {
                        return false;
                    }

                    continue;
                }

                if (argument.StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"알 수 없는 인수입니다: {argument}";
                    return false;
                }

                // 실제 경로 정규화와 파일/디렉터리 존재 여부는 모든 입력 형식을 아는 resolver가 한 곳에서 판정한다.
                request.InputPaths.Add(argument);
            }

            if (request.InputPaths.Count == 0)
            {
                error = "검사할 C# 파일 또는 디렉터리를 하나 이상 지정해야 합니다.";
                return false;
            }

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// --format 값 하나를 소비하고 지원 형식인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryReadFormat(string[] args, ref int index, CommandLineRequest request, out string error)
        {
            error = string.Empty;
            if (index + 1 >= args.Length)
            {
                error = "--format 뒤에는 text 또는 json이 필요합니다.";
                return false;
            }

            request.Format = args[++index].ToLowerInvariant();
            if (request.Format is "text" or "json")
            {
                return true;
            }

            error = "--format은 text 또는 json만 허용합니다.";
            return false;
        }
    }

}
