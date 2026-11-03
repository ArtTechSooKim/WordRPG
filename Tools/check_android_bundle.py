"""구글플레이에 올리기 전 AAB 검사 (#149). 표준 라이브러리 + Unity에 들어 있는 bundletool.
확인: 패키지 이름 · 대상 API 36 · 게임 표시 · 뒤로 가기 설정 · 권한 · 64비트 · 16KB 메모리 페이지(네이티브 라이브러리) · 내려받기 크기.
사용: python Tools/check_android_bundle.py Builds/Android/WordRPG-1.2.aab
"""
import os
import re
import struct
import subprocess
import sys
import zipfile

ANDROID = r"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\PlaybackEngines\AndroidPlayer"
JAVA = os.path.join(ANDROID, "OpenJDK", "bin", "java.exe")
BUNDLETOOL = os.path.join(ANDROID, "Tools", "bundletool-all-1.17.2.jar")
PACKAGE = "com.arttechsoo.wordrpg"
TARGET_API = 36
PAGE = 16384
BASE_LIMIT_MB = 200  # 게임 안내 문서의 기준 (일반 문서는 500MB) — 더 낮은 쪽으로 봄


def load_alignments(data):
    """64비트 ELF의 LOAD 구간 정렬값들"""
    if data[:4] != b"\x7fELF" or data[4] != 2:
        return None
    phoff = struct.unpack_from("<Q", data, 32)[0]
    phentsize, phnum = struct.unpack_from("<HH", data, 54)
    aligns = []
    for i in range(phnum):
        offset = phoff + i * phentsize
        if struct.unpack_from("<I", data, offset)[0] == 1:
            aligns.append(struct.unpack_from("<Q", data, offset + 48)[0])
    return aligns


def main(path):
    problems = []
    manifest = subprocess.run([JAVA, "-jar", BUNDLETOOL, "dump", "manifest", f"--bundle={path}"],
                              capture_output=True, text=True, encoding="utf-8", errors="replace").stdout

    def attribute(name):
        found = re.search(rf'{name}="([^"]*)"', manifest)
        return found.group(1) if found else None

    print("패키지", attribute("package"), "| 앱 버전", attribute("android:versionName"), "| 빌드 번호", attribute("android:versionCode"))
    print("최소 API", attribute("android:minSdkVersion"), "| 대상 API", attribute("android:targetSdkVersion"))
    print("appCategory", attribute("android:appCategory"), "| 뒤로 가기(enableOnBackInvokedCallback)", attribute("android:enableOnBackInvokedCallback"),
          "| 화면 방향", attribute("android:screenOrientation"))
    permissions = sorted(set(re.findall(r'uses-permission[^>]*android:name="([^"]+)"', manifest)))
    print("권한:", ", ".join(permissions))

    if attribute("package") != PACKAGE:
        problems.append("패키지 이름이 다름")
    if attribute("android:targetSdkVersion") != str(TARGET_API):
        problems.append(f"대상 API가 {TARGET_API}이 아님")
    if attribute("android:appCategory") not in ("game", "0"):  # 0 = game (dump는 번호로 보여 줌)
        problems.append("appCategory가 game이 아님 (안드로이드 16 큰 화면에서 세로 고정이 풀림)")
    if attribute("android:enableOnBackInvokedCallback") != "false":
        problems.append("뒤로 가기 설정 확인 필요 (enableOnBackInvokedCallback=false가 아님 — API 36에서 뒤로 가기 키가 안 올 수 있음)")
    if "com.android.vending.BILLING" not in permissions:
        problems.append("결제 권한(BILLING)이 없음")

    # 네이티브 라이브러리: 16KB 페이지
    with zipfile.ZipFile(path) as bundle:
        libs = [n for n in bundle.namelist() if n.endswith(".so")]
        arm64 = [n for n in libs if "/arm64-v8a/" in n]
        if not arm64:
            problems.append("64비트(arm64-v8a) 라이브러리가 없음")
        if any("/armeabi-v7a/" in n or "/x86/" in n for n in libs):
            print("※ 32비트 라이브러리도 들어 있음")
        bad = []
        for name in arm64:
            aligns = load_alignments(bundle.read(name))
            if aligns is None or min(aligns) < PAGE:
                bad.append(f"{os.path.basename(name)} ({min(aligns) if aligns else '?'})")
        print(f"arm64 라이브러리 {len(arm64)}개, 16KB 정렬 안 된 것: {bad or '없음'}")
        if bad:
            problems.append("16KB 페이지 미지원 라이브러리")

    size = os.path.getsize(path) / (1024 * 1024)
    print(f"AAB 파일 크기 {size:.1f} MB (기기별 내려받기 크기는 이보다 작음. 구글 기준 기본 모듈 {BASE_LIMIT_MB}~500MB)")
    if size > BASE_LIMIT_MB:
        problems.append(f"AAB가 {BASE_LIMIT_MB}MB를 넘음 — 내려받기 크기 확인 필요 (넘으면 Split Application Binary)")

    print("문제:", problems if problems else "없음")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
