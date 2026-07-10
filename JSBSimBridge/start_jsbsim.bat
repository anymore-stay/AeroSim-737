@echo off
REM ============================================================
REM  Start JSBSim realtime sim (Cessna 172p) for Unity
REM  UDP 5501: JSBSim -> Unity (state)
REM  TCP 5502: Unity  -> JSBSim (control)
REM
REM  Order: click Play in Unity FIRST, then run this file.
REM  Close this window to stop the sim.
REM ============================================================

set "JSBSIM_DIR=D:\jsbsim\JSBSim"
set "BRIDGE=%~dp0"
set "SCRIPT=%BRIDGE%b737_unity.xml"
set "OUTCFG=%BRIDGE%unity_output.xml"

echo ============================================
echo   JSBSim realtime sim starting (Boeing 737)
echo   State out UDP : 127.0.0.1:5501
echo   Control in TCP: 127.0.0.1:5502
echo   Close this window to stop the sim
echo ============================================
echo.

if not exist "%JSBSIM_DIR%\JSBSim.exe" (
    echo [ERROR] JSBSim.exe not found at: %JSBSIM_DIR%
    echo Edit JSBSIM_DIR on line 12 of this bat file.
    goto end
)
if not exist "%SCRIPT%" (
    echo [ERROR] Script not found: %SCRIPT%
    goto end
)

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

cd /d "%JSBSIM_DIR%"
"%JSBSIM_DIR%\JSBSim.exe" --realtime --root="%JSBSIM_DIR%" --script="%SCRIPT%" "%OUTCFG%"

echo.
echo ============================================
echo  JSBSim exited (exit code %errorlevel%)
echo ============================================

:end
echo.
pause
