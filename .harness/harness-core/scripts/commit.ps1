# commit_cmd 대상 스크립트.
# orchestrator(runners.run_commit)가 커밋 메시지를 마지막 인자로 넘긴다.
# 커밋 대상 repo 경로는 HARNESS_COMMIT_REPO_DIR env로 전달된다.
# exit code = committed 신호 (0 = 성공).
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Message
)

if ($env:HARNESS_COMMIT_REPO_DIR) {
    Set-Location $env:HARNESS_COMMIT_REPO_DIR
}

git add -A
git commit -m $Message
exit $LASTEXITCODE
