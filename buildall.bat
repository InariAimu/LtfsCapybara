@echo off
REM Build all non-test projects and the GUI, then collect artifacts into 'build' folder.
setlocal enabledelayedexpansion
set "ROOT=%~dp0"
set "BUILD_DIR=%ROOT%build"
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"
echo.
echo Building .NET projects (excluding projects with 'Test' in their path)...
for /R "%ROOT%" %%F in (*.csproj) do (
  echo %%~fF | findstr /I "\\Test\\\|\\Tests\\\|Test\.csproj" >nul
  if errorlevel 1 (
    echo Publishing %%~nF...
    dotnet publish "%%~fF" -c Release -o "%BUILD_DIR%\%%~nF"
  ) else (
    echo Skipping test project %%~nF
  )
)
echo.
echo Building ltfs-capybara-gui (npm run tauri build)...
pushd "%ROOT%ltfs-capybara-gui" || (echo GUI folder not found & exit /b 1)
if not exist node_modules (
  echo Installing npm dependencies...
  npm install || (echo npm install failed & popd & exit /b 1)
)
npm run tauri build || (echo GUI build failed & popd & exit /b 1)
popd
echo.
echo Copying GUI artifacts into build folder...
if exist "%ROOT%ltfs-capybara-gui\dist" (
  mkdir "%BUILD_DIR%\ltfs-capybara-gui\dist" >nul 2>&1
  xcopy "%ROOT%ltfs-capybara-gui\dist\*" "%BUILD_DIR%\ltfs-capybara-gui\dist\" /E /I /Y >nul
)
if exist "%ROOT%ltfs-capybara-gui\src-tauri\target\release\bundle" (
  mkdir "%BUILD_DIR%\ltfs-capybara-gui\bundle" >nul 2>&1
  xcopy "%ROOT%ltfs-capybara-gui\src-tauri\target\release\bundle\*" "%BUILD_DIR%\ltfs-capybara-gui\bundle\" /E /I /Y >nul
)
echo.
echo Build complete. Artifacts collected in %BUILD_DIR%
endlocal
exit /b 0