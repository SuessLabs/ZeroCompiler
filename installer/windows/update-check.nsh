!include "nsDialogs.nsh"

Var UpdateDialog
Var UpdateStatusLabel
Var UpdateCheckButton

Function UpdatePageCreate
  IfSilent skip
  !insertmacro MUI_HEADER_TEXT "Check for updates" "Optionally check for a newer Zero ${PRODUCT} installer."
  nsDialogs::Create 1018
  Pop $UpdateDialog

  ${If} $UpdateDialog == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 0 100% 42u "This package contains Zero ${PRODUCT} ${VERSION}.$\r$\nCheck GitHub for a newer stable installer, or click Next to install this version."

  Pop $0

  ${NSD_CreateButton} 0 48u 110u 16u "Check for updates"

  Pop $UpdateCheckButton

  ${NSD_OnClick} $UpdateCheckButton CheckForUpdates
  ${NSD_CreateLabel} 0 74u 100% 58u "No update check has been performed. An internet connection is needed only for checking."

  Pop $UpdateStatusLabel
  nsDialogs::Show
  Return
skip:
  Abort
FunctionEnd

Function CheckForUpdates
  Pop $0 ; nsDialogs passes the button handle.

  EnableWindow $UpdateCheckButton 0

  ${NSD_SetText} $UpdateStatusLabel "Checking GitHub for a newer installer..."
  System::Call 'user32::UpdateWindow(p $HWNDPARENT)'

  InitPluginsDir
  SetOutPath "$PLUGINSDIR"

  File /oname=Check-ZeroUpdate.ps1 "Check-ZeroUpdate.ps1"

  Delete "$PLUGINSDIR\update-result.ini"

  nsExec::ExecToStack /TIMEOUT=20000 '$\"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe$\" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $\"$PLUGINSDIR\Check-ZeroUpdate.ps1$\" -Product ${PRODUCT} -CurrentVersion ${VERSION} -ResultPath $\"$PLUGINSDIR\update-result.ini$\"'
  Pop $0
  Pop $1

  ${If} $0 == 0
    ReadINIStr $0 "$PLUGINSDIR\update-result.ini" "Update" "Status"
    ReadINIStr $1 "$PLUGINSDIR\update-result.ini" "Update" "LatestVersion"
  ${Else}
    StrCpy $0 "Unavailable"
  ${EndIf}

  ${If} $0 == "Available"
    ${NSD_SetText} $UpdateStatusLabel "Zero ${PRODUCT} $1 is available. This installer contains ${VERSION}. You can continue with this package or download the newer version."
    MessageBox MB_YESNO|MB_ICONINFORMATION "Zero ${PRODUCT} $1 is available. Open the release download page?" IDNO done
    ExecShell "open" "https://github.com/SuessLabs/ZeroCompiler/releases/latest"
  ${ElseIf} $0 == "Current"
    ${NSD_SetText} $UpdateStatusLabel "This package is current. Latest published Zero ${PRODUCT} installer: $1."
  ${ElseIf} $0 == "Missing"
    ${NSD_SetText} $UpdateStatusLabel "No matching stable Windows installer is published yet. Click Next to continue with this package."
  ${Else}
    ${NSD_SetText} $UpdateStatusLabel "Could not check for updates. You may be offline or GitHub may be unavailable. Click Next to continue with this package."
  ${EndIf}

done:
  EnableWindow $UpdateCheckButton 1
FunctionEnd
