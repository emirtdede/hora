; Hora installer script (NSIS 3.x, Unicode).
; Build: makensis /DVERSION=1.0.0 /DPUBLISH_DIR=..\publish\Hora installer\hora.nsi
; The application itself supports 73 languages; this page exposes every
; installer language that NSIS ships, which covers the same regional groups.

Unicode true
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"

!ifndef VERSION
  !define VERSION "1.0.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\publish\Hora"
!endif
!ifndef OUT_FILE
  !define OUT_FILE "..\dist\Hora-Setup-${VERSION}.exe"
!endif

!define APP_NAME "Hora"
!define APP_PUBLISHER "Vellium"
!define APP_EXE "Hora.exe"
!define APP_REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Hora"
!define APP_URL "https://github.com/emirtdede/hora"
!define APP_VELLIUM "https://vellium.dev"

Name "${APP_NAME}"
BrandingText "${APP_NAME} ${VERSION} - ${APP_PUBLISHER}"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Hora"
InstallDirRegKey HKCU "Software\Vellium\Hora" "InstallDir"
RequestExecutionLevel user
ShowInstDetails nevershow
ShowUninstDetails nevershow
XPStyle on

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (c) 2026 Vellium"
VIAddVersionKey "FileDescription" "Hora screen time tracker"
VIAddVersionKey "FileVersion" "${VERSION}"

!define MUI_ICON "..\assets\hora.ico"
!define MUI_UNICON "..\assets\hora.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "$(INSTALLER_RUN_TEXT)"
; The stock NSIS text repeats the product name with a doubled space, so the
; finish page uses its own catalog strings.
!define MUI_FINISHPAGE_TITLE "$(INSTALLER_FINISH_TITLE)"
!define MUI_FINISHPAGE_TEXT "$(INSTALLER_FINISH_TEXT)"
!define MUI_WELCOMEPAGE_TITLE "${APP_NAME} ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "$(INSTALLER_WELCOME_INTRO)$\r$\n$\r$\n$(INSTALLER_WELCOME_PRIVACY)$\r$\n$\r$\n$(INSTALLER_WELCOME_PUBLISHER)"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Generated from the application catalogs so the setup page speaks the same
; language as the app. Regenerate with scripts/make-installer-languages.mjs.
!include "generated\languages.nsh"

!insertmacro MUI_RESERVEFILE_LANGDLL

Function .onInit
  ; A silent install (winget, deployment tools) must never stop on a dialog.
  ${IfNot} ${AtLeastWin10}
    IfSilent win_ok
    MessageBox MB_ICONSTOP "$(INSTALLER_REQUIRES_WINDOWS10)"
    Abort
    win_ok:
  ${EndIf}

  SetRegView 64
  ReadRegStr $0 HKCU "${APP_REGKEY}" "InstallLocation"
  ${If} $0 == ""
    ReadRegStr $0 HKCU "Software\Vellium\Hora" "InstallDir"
  ${EndIf}
  ${If} $0 != ""
    IfSilent upgrade
    MessageBox MB_YESNO|MB_ICONQUESTION "$(INSTALLER_ALREADY_INSTALLED)" IDYES upgrade
    Abort
    upgrade:
      ; The old uninstaller removes only its own files; _?= keeps the process
      ; from deleting itself before it has finished.
      ExecWait '"$0\Uninstall.exe" /S _?=$0'
  ${EndIf}

  IfSilent lang_ready
  !insertmacro MUI_LANGDLL_DISPLAY
  lang_ready:
FunctionEnd

Section "${APP_NAME}" SecApp
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Vellium\Hora" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${APP_REGKEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${APP_REGKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${APP_REGKEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "${APP_REGKEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "${APP_REGKEY}" "URLInfoAbout" "${APP_URL}"
  WriteRegStr HKCU "${APP_REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${APP_REGKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${APP_REGKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${APP_REGKEY}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${APP_REGKEY}" "EstimatedSize" "$0"
SectionEnd

Section "$(INSTALLER_SECTION_NAME_START)" SecStart
  CreateDirectory "$SMPROGRAMS\Hora"
  CreateShortCut "$SMPROGRAMS\Hora\Hora.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\Hora\Uninstall Hora.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

Section "$(INSTALLER_SECTION_NAME_DESKTOP)" SecDesktop
  CreateShortCut "$DESKTOP\Hora.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "$(INSTALLER_SECTION_NAME_STARTUP)" SecStartup
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Hora" '"$INSTDIR\${APP_EXE}" --minimized'
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} $(INSTALLER_SECTION_APP)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStart} $(INSTALLER_SECTION_START)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(INSTALLER_SECTION_DESKTOP)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartup} $(INSTALLER_SECTION_STARTUP)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function un.onInit
  ; The running app holds the executable and the database, so close it first.
  nsExec::ExecToLog 'taskkill /IM ${APP_EXE} /F'
  Pop $0

  ; Silent uninstall (winget, Settings > Apps) runs without any prompt.
  IfSilent uninstall_now
  MessageBox MB_YESNO|MB_ICONQUESTION "$(INSTALLER_UNINSTALL_CONFIRM)" IDYES uninstall_now
  Abort
  uninstall_now:
FunctionEnd

Section "Uninstall"
  Delete "$DESKTOP\Hora.lnk"
  Delete "$SMPROGRAMS\Hora\Hora.lnk"
  Delete "$SMPROGRAMS\Hora\Uninstall Hora.lnk"
  RMDir "$SMPROGRAMS\Hora"

  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Hora"
  DeleteRegKey HKCU "${APP_REGKEY}"
  DeleteRegKey HKCU "Software\Vellium\Hora"

  RMDir /r "$INSTDIR"
SectionEnd
