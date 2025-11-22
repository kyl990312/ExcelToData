import os
import subprocess
from github import Github

# 1️⃣ 환경 변수 가져오기
GITHUB_TOKEN = os.environ["GITHUB_TOKEN"]
REPO_NAME = os.environ["GITHUB_REPOSITORY"]
PR_NUMBER = int(os.environ["PR_NUMBER"])
SHA_BEFORE = os.environ["GITHUB_SHA_BEFORE"]
SHA_AFTER = os.environ["GITHUB_SHA"]

# 2️⃣ 변경된 파일 목록 가져오기
changed_files = subprocess.check_output(
    ["git", "diff", "--name-only", SHA_BEFORE, SHA_AFTER]
).decode().splitlines()

# 3️⃣ 변경 파일만 Copilot CLI로 리뷰
reviews = []
for file_path in changed_files:
    # C# 파일만 리뷰
    if not file_path.endswith(".cs"):
        continue

    diff_text = subprocess.check_output(["git", "diff", file_path]).decode()
    if not diff_text.strip():
        continue

    # Copilot CLI 호출
    copilot_result = subprocess.check_output(
        ["copilot", "review", "--input", diff_text]
    ).decode()
    reviews.append((file_path, copilot_result))

# 4️⃣ GitHub PR에 코멘트 업로드
g = Github(GITHUB_TOKEN)
repo = g.get_repo(REPO_NAME)
pr = repo.get_pull(PR_NUMBER)

for file_path, review_text in reviews:
    pr.create_issue_comment(
        f"**Copilot Review for {file_path}**\n```\n{review_text}\n```"
    )

print(f"✅ Posted {len(reviews)} Copilot review comments")
