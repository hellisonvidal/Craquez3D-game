#!/usr/bin/env python3
"""
Gera o instalador gráfico do Windows (Craquez3D-vX.Y.Z-setup.exe) usando NSIS (makensis) em Linux/Windows.
- Cria ícone .ico a partir de Assets/Craquez/Textures/Icone/Icone_App.png (sem depender de pacotes externos);
- Empacota toda a pasta do build Windows gerado pela Unity (Craquez3D.exe, Craquez3D_Data, MonoBleedingEdge, UnityPlayer.dll...);
- Interface em Português (Brasil), instalação por usuário em %LOCALAPPDATA%\\Programs\\Craquez 3D (sem exigir Admin),
  atalhos na Área de Trabalho e no Menu Iniciar, registro no Painel de Controle (Aplicativos Instalados) e desinstalador.
"""
import argparse
import os
import shutil
import struct
import subprocess
import tempfile


def png_to_ico(png_path: str, ico_path: str) -> bool:
    if not png_path or not os.path.isfile(png_path):
        return False
    try:
        from PIL import Image
        img = Image.open(png_path).convert("RGBA")
        img.save(ico_path, format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
        return True
    except Exception:
        # Fallback puro da stdlib: encapsula PNG 256x256 num contêiner ICO válido (suportado desde o Windows Vista+)
        data = open(png_path, "rb").read()
        if data[:8] != b"\x89PNG\r\n\x1a\n":
            return False
        header = struct.pack("<HHH", 0, 1, 1)
        entry = struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(data), 6 + 16)
        with open(ico_path, "wb") as f:
            f.write(header + entry + data)
        return True


def calc_size_kb(folder: str) -> int:
    total = 0
    for root, _, files in os.walk(folder):
        for fn in files:
            try:
                total += os.path.getsize(os.path.join(root, fn))
            except OSError:
                pass
    return max(1, total // 1024)


def build_installer(build_dir: str, version: str, output_exe: str, icon_png: str) -> None:
    build_dir = os.path.abspath(build_dir)
    output_exe = os.path.abspath(output_exe)
    exe_file = os.path.join(build_dir, "Craquez3D.exe")
    if not os.path.isfile(exe_file):
        raise SystemExit(f"Erro: executável principal não encontrado em {exe_file}")

    os.makedirs(os.path.dirname(output_exe), exist_ok=True)
    size_kb = calc_size_kb(build_dir)

    parts = (version.split(".") + ["0", "0", "0", "0"])[:4]
    vi_version = ".".join(str(int(x)) if x.isdigit() else "0" for x in parts)

    with tempfile.TemporaryDirectory(prefix="craquez_nsis_") as tmp:
        ico_path = os.path.join(tmp, "Craquez3D.ico")
        has_icon = png_to_ico(icon_png, ico_path)
        if has_icon:
            shutil.copy2(ico_path, os.path.join(build_dir, "Craquez3D.ico"))

        icon_defs = ""
        uninstall_icon_reg = '$INSTDIR\\Craquez3D.exe,0'
        if has_icon:
            icon_defs = f'!define MUI_ICON "{ico_path}"\n!define MUI_UNICON "{ico_path}"\nIcon "{ico_path}"\nUninstallIcon "{ico_path}"'
            uninstall_icon_reg = '$INSTDIR\\Craquez3D.ico'

        nsi = f"""Unicode True
SetCompressor /SOLID lzma
!include "MUI2.nsh"

Name "Craquez 3D {version}"
OutFile "{output_exe}"
InstallDir "$LOCALAPPDATA\\Programs\\Craquez 3D"
InstallDirRegKey HKCU "Software\\Craquez\\Craquez3D" "Install_Dir"
RequestExecutionLevel user
BrandingText "Craquez 3D v{version}"

VIProductVersion "{vi_version}"
VIAddVersionKey "ProductName" "Craquez 3D"
VIAddVersionKey "CompanyName" "Craquez"
VIAddVersionKey "FileDescription" "Instalador do Craquez 3D v{version}"
VIAddVersionKey "FileVersion" "{version}"
VIAddVersionKey "ProductVersion" "{version}"
VIAddVersionKey "LegalCopyright" "Craquez 3D"

!define MUI_ABORTWARNING
{icon_defs}
!define MUI_FINISHPAGE_RUN "$INSTDIR\\Craquez3D.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Jogar Craquez 3D agora"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "PortugueseBR"

Section "Craquez 3D" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "{build_dir}\\*.*"

  WriteRegStr HKCU "Software\\Craquez\\Craquez3D" "Install_Dir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\\Craquez 3D"
  CreateShortcut "$SMPROGRAMS\\Craquez 3D\\Craquez 3D.lnk" "$INSTDIR\\Craquez3D.exe" "" "{uninstall_icon_reg}" 0
  CreateShortcut "$SMPROGRAMS\\Craquez 3D\\Desinstalar Craquez 3D.lnk" "$INSTDIR\\Uninstall.exe"
  CreateShortcut "$DESKTOP\\Craquez 3D.lnk" "$INSTDIR\\Craquez3D.exe" "" "{uninstall_icon_reg}" 0

  WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "DisplayName" "Craquez 3D"
  WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "DisplayVersion" "{version}"
  WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "Publisher" "Craquez"
  WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "DisplayIcon" "{uninstall_icon_reg}"
  WriteRegStr HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "UninstallString" '"$INSTDIR\\Uninstall.exe"'
  WriteRegDWORD HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "NoModify" 1
  WriteRegDWORD HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "NoRepair" 1
  WriteRegDWORD HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D" "EstimatedSize" {size_kb}
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\\Craquez 3D.lnk"
  Delete "$SMPROGRAMS\\Craquez 3D\\Craquez 3D.lnk"
  Delete "$SMPROGRAMS\\Craquez 3D\\Desinstalar Craquez 3D.lnk"
  RMDir "$SMPROGRAMS\\Craquez 3D"

  RMDir /r "$INSTDIR"

  DeleteRegKey HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Craquez3D"
  DeleteRegKey HKCU "Software\\Craquez\\Craquez3D"
SectionEnd
"""
        nsi_path = os.path.join(tmp, "installer.nsi")
        with open(nsi_path, "w", encoding="utf-8-sig") as f:
            f.write(nsi)

        subprocess.run(["makensis", "-V2", nsi_path], check=True)

    if not os.path.isfile(output_exe) or os.path.getsize(output_exe) < 32768:
        raise SystemExit("Erro: o instalador gerado está ausente ou inválido.")
    print(f"[Craquez 3D] Instalador Windows gerado: {output_exe} ({os.path.getsize(output_exe) / (1024 * 1024):.2f} MB)")


def main() -> None:
    ap = argparse.ArgumentParser(description="Gera o instalador Windows (.exe) do Craquez 3D via NSIS.")
    ap.add_argument("--build-dir", required=True, help="Pasta com Craquez3D.exe e Craquez3D_Data")
    ap.add_argument("--version", required=True, help="Versão sem 'v' (ex.: 1.7.0)")
    ap.add_argument("--output", required=True, help="Caminho do instalador .exe de saída")
    ap.add_argument("--icon-png", default="Assets/Craquez/Textures/Icone/Icone_App.png", help="PNG do ícone do jogo")
    args = ap.parse_args()
    build_installer(args.build_dir, args.version, args.output, args.icon_png)


if __name__ == "__main__":
    main()
