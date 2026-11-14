!define PRODUCT "Ide"
!define VERSION "${IDE_VERSION}"

Name "Zero IDE ${VERSION}"
OutFile "${OUTPUT}"
!include "common.nsh"

; Evaluate the PATH option before running the embedded CLI setup.
Section /o "Add Zero CLI to the system PATH (open a new terminal)" Path
SectionEnd

Section "Zero IDE and Zero CLI (required)" Core
  SectionIn RO
  Call ValidateRoot

  InitPluginsDir
  SetOutPath "$PLUGINSDIR"

  File /oname=Zero.Cli-Setup.exe "${CLI_SETUP}"
  
  StrCpy $1 ""
  SectionGetFlags ${Path} $0
  IntOp $0 $0 & ${SF_SELECTED}

  ${If} $0 != 0
    StrCpy $1 "/ADDPATH=1"
  ${EndIf}
  
  ExecWait '$\"$PLUGINSDIR\Zero.Cli-Setup.exe$\" /S $1 /D=$INSTDIR' $0
  
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "CLI installation failed. IDE installation cannot continue." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}

  IfFileExists "$INSTDIR\bin\zero.exe" +4
    MessageBox MB_ICONSTOP "The required CLI was not found in the selected installation folder." /SD IDOK
    SetErrorLevel 1
    Quit

  SetOutPath "$INSTDIR"
  File /r "${STAGE}\ide\*"

  CreateDirectory "$SMPROGRAMS\SuessLabs Zero"
  SetOutPath "$INSTDIR\ide"
  
  CreateShortcut "$SMPROGRAMS\SuessLabs Zero\Zero IDE.lnk" "$INSTDIR\ide\Zero.Ide.exe"
  !insertmacro RegisterProduct
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\SuessLabs Zero\Zero IDE.lnk"
  RMDir "$SMPROGRAMS\SuessLabs Zero"
  !include "${STAGE}\uninstall-ide.nsh"
  !insertmacro UnregisterProduct
SectionEnd
