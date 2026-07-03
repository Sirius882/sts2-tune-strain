@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: 切换到脚本所在目录，避免从别的工作目录启动时找不到 .git
cd /d "%~dp0"

:: 检查是否在 Git 仓库中
git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo 当前目录不是 Git 仓库，请把本文件放在仓库根目录下。
    pause
    exit /b
)

:: 获取提交信息：如果拖拽文件或双击时带参数，则使用第一个参数；否则使用当前时间
if "%~1"=="" (
    for /f "tokens=1-3 delims=/- " %%a in ('date /t') do (
        set today=%%a-%%b-%%c
    )
    for /f "tokens=1-2 delims=: " %%a in ('time /t') do (
        set now=%%a:%%b
    )
    set commit_msg=%today% %now%
) else (
    set commit_msg=%~1
)

echo 添加所有更改...
git add .

echo 提交更改...
git commit -m "%commit_msg%"

if errorlevel 1 (
    echo 提交失败（可能没有需要提交的更改），退出。
    pause
    exit /b
)

echo 推送到远程仓库...
git push origin main

if errorlevel 1 (
    echo 推送失败，请检查网络或凭证。
    pause
    exit /b
)

echo ========== 推送成功！==========
echo 提交信息：%commit_msg%
pause