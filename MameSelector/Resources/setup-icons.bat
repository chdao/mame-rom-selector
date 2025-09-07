@echo off
echo MAME ROM Selector Icon Setup
echo ============================
echo.
echo This script will help you organize your icon files.
echo.
echo Please ensure you have the following files ready:
echo - app.ico (main application icon)
echo - app-16.png, app-32.png, app-48.png, app-64.png, app-128.png, app-256.png
echo.
echo Place all icon files in this directory, then run this script.
echo.
pause

echo.
echo Checking for icon files...
echo.

if exist "app.ico" (
    echo ✓ app.ico found
) else (
    echo ✗ app.ico missing - this is required!
)

if exist "app-16.png" (
    echo ✓ app-16.png found
) else (
    echo ✗ app-16.png missing
)

if exist "app-32.png" (
    echo ✓ app-32.png found
) else (
    echo ✗ app-32.png missing
)

if exist "app-48.png" (
    echo ✓ app-48.png found
) else (
    echo ✗ app-48.png missing
)

if exist "app-64.png" (
    echo ✓ app-64.png found
) else (
    echo ✗ app-64.png missing
)

if exist "app-128.png" (
    echo ✓ app-128.png found
) else (
    echo ✗ app-128.png missing
)

if exist "app-256.png" (
    echo ✓ app-256.png found
) else (
    echo ✗ app-256.png missing
)

echo.
echo Icon setup complete!
echo.
echo Next steps:
echo 1. Build the project: dotnet build
echo 2. Run the application to see the icon
echo 3. Commit the icon files to git
echo.
pause
