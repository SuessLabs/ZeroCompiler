!define PRODUCT "Cli"
!define CLI_INSTALLER
!define VERSION "${CLI_VERSION}"

Name "Zero CLI ${VERSION}"
OutFile "${OUTPUT}"
!include "common.nsh"

Section "Zero CLI, headers, samples and documentation (required)" Core
  SectionIn RO
  Call ValidateRoot
  SetOutPath "$INSTDIR"
  File /r "${STAGE}\cli\*"
  !insertmacro RegisterProduct
SectionEnd

Section /o "Add Zero CLI to the system PATH (open a new terminal)" Path
  Call AddPath
SectionEnd

Function AddPath
  ExecWait '$\"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe$\" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $\"$INSTDIR\bin\Set-ZeroPath.ps1$\" -Action Add' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Zero was installed, but adding it to PATH failed. Run setup again to retry." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}
FunctionEnd

Function .onInstSuccess
  ${GetParameters} $0
  ${GetOptions} $0 "/ADDPATH=" $1
  ${If} $1 == "1"
    Call AddPath
  ${EndIf}
FunctionEnd

Section "Uninstall"
  ReadRegStr $0 HKLM "Software\SuessLabs\Zero\Ide" "InstallLocation"

  ${If} $0 != ""
    MessageBox MB_ICONSTOP "Uninstall Zero IDE before removing its required CLI." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}

  ExecWait '$\"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe$\" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $\"$INSTDIR\bin\Set-ZeroPath.ps1$\" -Action Remove' $0
  
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Removing Zero from PATH failed. No files were removed; retry uninstall." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}

  !include "${STAGE}\uninstall-cli.nsh"
  !insertmacro UnregisterProduct
SectionEnd
