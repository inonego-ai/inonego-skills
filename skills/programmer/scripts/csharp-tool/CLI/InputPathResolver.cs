/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : InputPathResolver.cs
수정일 : 2026-09-17

# 설명
파일과 디렉터리 입력을 실제 검사 대상 C# 파일 목록으로 해석한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 입력 경로 집합을 중복 없는 C# 파일 목록으로 해석한다.
    /// </summary>
    // ============================================================
    internal static class InputPathResolver
    {

    #region 입력 해석

        // ------------------------------------------------------------
        /// <summary>
        /// 파일과 디렉터리 입력을 정렬된 C# 파일 목록으로 확장한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool TryResolve(IReadOnlyList<string> inputPaths, out List<string> filePaths, out string error)
        {
            filePaths = new();
            error = string.Empty;
            HashSet<string> knownPaths = new(StringComparer.OrdinalIgnoreCase);

            // 각 입력은 파일이면 그대로, 디렉터리면 재귀적으로 C# 파일까지 확장한다.
            foreach (string inputPath in inputPaths)
            {
                if (File.Exists(inputPath))
                {
                    if (!inputPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"C# 파일 또는 디렉터리만 입력할 수 있습니다: {inputPath}";
                        return false;
                    }

                    AddFile(inputPath, knownPaths, filePaths);
                    continue;
                }

                if (Directory.Exists(inputPath))
                {
                    if (IsExcludedDirectory(inputPath) || IsReparsePoint(inputPath))
                    {
                        continue;
                    }

                    CollectDirectory(inputPath, knownPaths, filePaths);
                    continue;
                }

                error = $"입력 경로를 찾을 수 없습니다: {inputPath}";
                return false;
            }

            // 입력 순서나 디렉터리 열거 순서에 관계없이 결과 순서를 결정적으로 고정한다.
            filePaths.Sort(StringComparer.OrdinalIgnoreCase);
            if (filePaths.Count == 0)
            {
                error = "입력 경로에서 C# 파일을 찾지 못했습니다.";
                return false;
            }

            return true;
        }

    #endregion

    #region 경로 수집과 제외

        // ------------------------------------------------------------
        /// <summary>
        /// 디렉터리를 재귀 순회해 bin/obj를 제외한 C# 파일을 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CollectDirectory(string rootPath, HashSet<string> knownPaths, List<string> filePaths)
        {
            Stack<string> pendingDirectories = new();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count > 0)
            {
                string currentPath = pendingDirectories.Pop();

                // 현재 디렉터리의 파일부터 수집해 명시적 파일 입력과 같은 중복 제거 경로를 사용한다.
                foreach (string filePath in Directory.EnumerateFiles(currentPath, "*.cs", SearchOption.TopDirectoryOnly))
                {
                    AddFile(filePath, knownPaths, filePaths);
                }

                // 빌드 산출물과 재분석할 필요가 없는 링크 디렉터리는 재귀 대상에서 제외한다.
                foreach (string childPath in Directory.EnumerateDirectories(currentPath))
                {
                    if (IsExcludedDirectory(childPath) || IsReparsePoint(childPath))
                    {
                        continue;
                    }

                    pendingDirectories.Push(childPath);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이미 수집한 경로가 아니면 정규화해 결과 목록에 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddFile(string filePath, HashSet<string> knownPaths, List<string> filePaths)
        {
            string fullPath = Path.GetFullPath(filePath);
            if (knownPaths.Add(fullPath))
            {
                filePaths.Add(fullPath);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 빌드 산출물 디렉터리인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsExcludedDirectory(string path)
        {
            string name = Path.GetFileName(path);
            bool isBin = name.Equals("bin", StringComparison.OrdinalIgnoreCase);
            bool isObj = name.Equals("obj", StringComparison.OrdinalIgnoreCase);
            return isBin || isObj;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 재귀 순회에서 다시 들어갈 수 있는 링크 디렉터리인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsReparsePoint(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }

    #endregion

    }

}
