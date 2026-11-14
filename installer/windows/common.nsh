Unicode true
RequestExecutionLevel admin
ManifestDPIAware true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"
!include "x64.nsh"

!if "${PRODUCT}" == "Cli"
  !define PRODUCT_DIR "bin"
!else
  !define PRODUCT_DIR "ide"
!endif

InstallDir "$PROGRAMFILES64\SuessLabs\Zero"
InstallDirRegKey HKLM "Software\SuessLabs\Zero\Cli" "InstallLocation"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "This installer requires 64-bit Windows."
    SetErrorLevel 1
    Quit
  ${EndIf}
  
  SetRegView 64
  SetShellVarContext all

  Call ValidateRoot
  
  ReadRegStr $0 HKLM "Software\SuessLabs\Zero\${PRODUCT}" "DisplayVersion"

  ${If} $0 != ""
    ${VersionCompare} "$0" "${VERSION}" $1
    ${If} $1 == 1
      IfSilent +2
      MessageBox MB_ICONINFORMATION "A newer ${PRODUCT} version is already installed."
!ifdef CLI_INSTALLER
      ${GetParameters} $0
      ${GetOptions} $0 "/ADDPATH=" $1
      ${If} $1 == "1"
        Call AddPath
      ${EndIf}
!endif
      SetErrorLevel 0
      Quit
    ${EndIf}
  ${EndIf}
FunctionEnd

Function ValidateRoot
  ReadRegStr $0 HKLM "Software\SuessLabs\Zero\Cli" "InstallLocation"
  ${If} $0 != ""
  ${AndIf} $0 != $INSTDIR
    MessageBox MB_ICONSTOP "Zero CLI is installed at $0. Use the existing Zero folder, or uninstall IDE and CLI before relocating." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}

  ReadRegStr $0 HKLM "Software\SuessLabs\Zero\Ide" "InstallLocation"
  
  ${If} $0 != ""
  ${AndIf} $0 != $INSTDIR
    MessageBox MB_ICONSTOP "Zero IDE is installed at $0. Use the existing Zero folder, or uninstall IDE and CLI before relocating." /SD IDOK
    SetErrorLevel 1
    Quit
  ${EndIf}
FunctionEnd

Function .onVerifyInstDir
  ReadRegStr $0 HKLM "Software\SuessLabs\Zero\Cli" "InstallLocation"

  ${If} $0 != ""
  ${AndIf} $0 != $INSTDIR
    ; Both products must share one root; moving requires uninstalling first.
    Abort
  ${EndIf}

  ReadRegStr $0 HKLM "Software\SuessLabs\Zero\Ide" "InstallLocation"

  ${If} $0 != ""
  ${AndIf} $0 != $INSTDIR
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  SetRegView 64
  SetShellVarContext all
  ; Uninstallers live with their product; shared payload paths use the parent root.
  ${GetParent} "$INSTDIR" $INSTDIR
FunctionEnd

!macro RegisterProduct
  WriteRegStr HKLM "Software\SuessLabs\Zero\${PRODUCT}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\SuessLabs\Zero\${PRODUCT}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "DisplayName" "Zero ${PRODUCT}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "Publisher" "SuessLabs"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "UninstallString" '$\"$INSTDIR\${PRODUCT_DIR}\Uninstall-Zero.${PRODUCT}.exe$\"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "QuietUninstallString" '$\"$INSTDIR\${PRODUCT_DIR}\Uninstall-Zero.${PRODUCT}.exe$\" /S'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}" "NoRepair" 1
  WriteUninstaller "$INSTDIR\${PRODUCT_DIR}\Uninstall-Zero.${PRODUCT}.exe"
!macroend

!macro UnregisterProduct
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SuessLabs.Zero.${PRODUCT}"
  DeleteRegKey HKLM "Software\SuessLabs\Zero\${PRODUCT}"
  Delete "$INSTDIR\${PRODUCT_DIR}\Uninstall-Zero.${PRODUCT}.exe"
  RMDir "$INSTDIR\${PRODUCT_DIR}"
  RMDir "$INSTDIR"
!macroend
