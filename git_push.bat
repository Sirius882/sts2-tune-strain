@echo off
setlocal EnableExtensions EnableDelayedExpansion

cd /d "%~dp0"

git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo ERROR: this directory is not a Git work tree.
    exit /b 1
)

for /f "delims=" %%b in ('git branch --show-current') do set "current_branch=%%b"
if "%current_branch%"=="" (
    echo ERROR: cannot determine current branch.
    exit /b 1
)

if "%~1"=="" (
    for /f "tokens=1-3 delims=/- " %%a in ('date /t') do set "today=%%a-%%b-%%c"
    for /f "tokens=1-2 delims=: " %%a in ('time /t') do set "now=%%a:%%b"
    set "commit_msg=%today% %now%"
) else (
    set "commit_msg=%~1"
)

echo Adding changes...
git add .
if errorlevel 1 exit /b 1

echo Committing changes...
git commit -m "%commit_msg%"
if errorlevel 1 (
    git diff --cached --quiet
    if errorlevel 1 (
        echo ERROR: commit failed with staged changes still present.
        exit /b 1
    )
    echo No new commit was created. Continuing to push existing commits.
)

echo Pushing to origin/%current_branch%...
git push origin %current_branch%
if errorlevel 1 (
    echo ERROR: push failed.
    exit /b 1
)

echo Push succeeded.
echo Commit message: %commit_msg%
