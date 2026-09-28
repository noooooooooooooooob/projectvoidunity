using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectVoid.Tests
{
    /// <summary>에디터를 연 채로 MCP 에서 테스트를 돌리고 결과를 파일로 남긴다 (커맨드라인 러너는 프로젝트 잠금 때문에 못 쓴다).</summary>
    [InitializeOnLoad]
    public static class TestBridge
    {
        public const string ResultPath = "Temp/ProjectVoidTestResults.txt";

        // PlayMode 실행은 도메인 리로드를 거치므로 리로드마다 콜백을 다시 건다.
        static TestBridge()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new ResultWriter());
        }

        public static void Run(TestMode mode, string filter)
        {
            if (File.Exists(ResultPath))
            {
                File.Delete(ResultPath);
            }
            // 수정된 씬이 있으면 러너가 저장 확인창을 띄워 MCP 호출이 멈춘다. 저장 없이 빈 씬으로 바꾼다 (2026-09-28 사용자 결정).
            if (AnySceneDirty())
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            var testFilter = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(filter))
            {
                testFilter.groupNames = new[] { filter };
            }
            var settings = new ExecutionSettings(testFilter) { runSynchronously = mode == TestMode.EditMode };
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(settings);
        }

        private static bool AnySceneDirty()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                int passed = 0, failed = 0, skipped = 0;
                var failures = new StringBuilder();
                Collect(result, ref passed, ref failed, ref skipped, failures);
                File.WriteAllText(ResultPath, $"PASSED {passed} / FAILED {failed} / SKIPPED {skipped}\n{failures}");
            }

            private static void Collect(ITestResultAdaptor node, ref int passed, ref int failed, ref int skipped, StringBuilder failures)
            {
                if (node.HasChildren)
                {
                    foreach (ITestResultAdaptor child in node.Children)
                    {
                        Collect(child, ref passed, ref failed, ref skipped, failures);
                    }
                    return;
                }
                switch (node.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed:
                        failed++;
                        failures.AppendLine($"FAIL {node.FullName}\n  {node.Message}");
                        break;
                    default: skipped++; break;
                }
            }
        }
    }
}
