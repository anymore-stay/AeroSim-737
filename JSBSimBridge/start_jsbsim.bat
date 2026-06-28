@echo off
REM ============================================================
REM  Start JSBSim realtime sim (Boeing 737) for Unity
REM  UDP 5501: JSBSim -> Unity (state)
REM  TCP 5502: Unity  -> JSBSim (control)
REM
REM  Order: click Play in Unity FIRST, then run this file.
REM  Close this window to stop the sim.
REM
REM  Path configuration:
REM  1. Recommended: set a machine-level or terminal-level JSBSIM_DIR env var.
REM  2. Fallback: edit DEFAULT_JSBSIM_DIR below for your local machine.
REM ============================================================

set "DEFAULT_JSBSIM_DIR=D:\Software\JSBSim"
if not defined JSBSIM_DIR set "JSBSIM_DIR=%DEFAULT_JSBSIM_DIR%"
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
    echo You can either:
    echo   1. set JSBSIM_DIR before running this script, or
    echo   2. edit DEFAULT_JSBSIM_DIR in start_jsbsim.bat, or
    echo   3. install JSBSim to the default path above.
    goto end
)
if not exist "%SCRIPT%" (
    echo [ERROR] Script not found: %SCRIPT%
    goto end
)

cd /d "%JSBSIM_DIR%"
"%JSBSIM_DIR%\JSBSim.exe" --realtime --root="%JSBSIM_DIR%" --script="%SCRIPT%" "%OUTCFG%"

echo.
echo ============================================
echo  JSBSim exited (exit code %errorlevel%)
echo ============================================

:end
echo.
pause
