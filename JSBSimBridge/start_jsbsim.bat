@echo off
REM ============================================================
REM  Start JSBSim realtime sim (Cessna 172p) for Unity
REM  UDP 5501: JSBSim -> Unity (state)
REM  TCP 5502: Unity  -> JSBSim (control)
REM
REM  Order: click Play in Unity FIRST, then run this file.
REM  Close this window to stop the sim.
REM ============================================================

set "DEFAULT_JSBSIM_DIR=D:\jsbsim\JSBSim"
if not defined JSBSIM_DIR set "JSBSIM_DIR=%DEFAULT_JSBSIM_DIR%"
set "JSBSIM_ROOT=%JSBSIM_DIR%"
set "JSBSIM_EXE=%JSBSIM_DIR%\JSBSim.exe"
for %%F in ("%JSBSIM_DIR%") do if /I "%%~xF"==".exe" (
    set "JSBSIM_EXE=%JSBSIM_DIR%"
    set "JSBSIM_ROOT=%%~dpF"
)
if "%JSBSIM_ROOT:~-1%"=="\" set "JSBSIM_ROOT=%JSBSIM_ROOT:~0,-1%"
set "BRIDGE=%~dp0"
set "SCRIPT=%BRIDGE%b737_unity.xml"
set "OUTCFG=%BRIDGE%unity_output.xml"
set "INIT_SRC=%BRIDGE%unity_air.xml"
set "INIT_DST=%JSBSIM_ROOT%\aircraft\737\unity_air.xml"

echo ============================================
echo   JSBSim realtime sim starting (Boeing 737)
echo   State out UDP : 127.0.0.1:5501
echo   Control in TCP: 127.0.0.1:5502
echo   Close this window to stop the sim
echo ============================================
echo.

if not exist "%JSBSIM_EXE%" (
    echo [ERROR] JSBSim.exe not found: %JSBSIM_EXE%
    echo Set JSBSIM_DIR to either the JSBSim folder or JSBSim.exe.
    echo Or edit DEFAULT_JSBSIM_DIR near the top of this bat file.
    goto end
)
if not exist "%SCRIPT%" (
    echo [ERROR] Script not found: %SCRIPT%
    goto end
)
if not exist "%INIT_SRC%" (
    echo [ERROR] Initial condition file not found: %INIT_SRC%
    goto end
)
if not exist "%JSBSIM_ROOT%\aircraft\737" (
    echo [ERROR] JSBSim 737 aircraft folder not found: %JSBSIM_ROOT%\aircraft\737
    goto end
)

echo Syncing initial condition:
echo   %INIT_SRC%
echo   -^> %INIT_DST%
copy /Y "%INIT_SRC%" "%INIT_DST%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to sync initial condition file.
    goto end
)
echo.

set "UNITY_WAIT_SECONDS=30"
echo Waiting for Unity UDP listener on port 5501...
for /L %%I in (1,1,%UNITY_WAIT_SECONDS%) do (
    powershell -NoProfile -Command "if (Get-NetUDPEndpoint -LocalPort 5501 -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
    if not errorlevel 1 goto unity_ready
    timeout /t 1 /nobreak >nul
)

echo [ERROR] Unity is not listening on UDP port 5501 after %UNITY_WAIT_SECONDS% seconds.
echo Enter Play Mode and confirm JsbsimBridge is enabled, then try again.
goto end

:unity_ready
echo Unity UDP listener is ready.
echo.

cd /d "%JSBSIM_ROOT%"
"%JSBSIM_EXE%" --realtime --root="%JSBSIM_ROOT%" --script="%SCRIPT%" "%OUTCFG%"

echo.
echo ============================================
echo  JSBSim exited (exit code %errorlevel%)
echo ============================================

:end
echo.
pause
