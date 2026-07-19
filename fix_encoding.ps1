# UTF-8 BOM encoding fix script

Write-Host "Starting UTF-8 conversion..." -ForegroundColor Green

$replacements = @{
    "Ŭ����" = "클래스"
    "���" = "파일"
    "���丮" = "디렉터리"
    "���ӽ����̽�" = "네임스페이스"
    "Ȯ����" = "확장자"
    "����" = "패턴"
    "����" = "설정"
    "����" = "여부"
    "���̳ʸ�" = "바이너리"
    "�ε�" = "로드"
    "�ڵ�" = "코드"
    "���" = "경로"
    "���" = "출력"
    "���̺�" = "테이블"
    "������" = "매니저"
    "�ܺ�" = "외부"
    "����" = "저장"
    "���·�" = "형태로"
    "���?" = "생성"
    "�����ϴ� ���ɾ�" = "추출하는 명령어"
    "�ʵ�" = "필드"
    "����ȭ��" = "직렬화에"
    "����" = "포함"
    "�����?" = "실행자"
    "��" = "지원"
    "�߰�" = "추가"
}

$extensions = @("*.cs")
$count = 0

Get-ChildItem -Path . -Recurse -Include $extensions -Exclude "bin","obj" | ForEach-Object {
    $file = $_

    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding Default

        $changed = $false
        foreach ($pattern in $replacements.Keys) {
            if ($content -match [regex]::Escape($pattern)) {
                $content = $content -replace [regex]::Escape($pattern), $replacements[$pattern]
                $changed = $true
            }
        }

        if ($changed) {
            [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
            Write-Host "Fixed: $($file.Name)" -ForegroundColor Yellow
            $count++
        }
    }
    catch {
        Write-Host "Error: $($file.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n$count files fixed!" -ForegroundColor Cyan
