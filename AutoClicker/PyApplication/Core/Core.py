import ctypes
from ctypes import wintypes
from time import monotonic

class MouseInput(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG), ("data", wintypes.DWORD), ("flags", wintypes.DWORD), ("time", wintypes.DWORD), ("extraInfo", ctypes.c_size_t)]

class Input(ctypes.Structure):
    _fields_ = [("type", wintypes.DWORD), ("mouse", MouseInput)]

class Luid(ctypes.Structure):
    _fields_ = [("low", wintypes.DWORD), ("high", wintypes.LONG)]

class TokenPrivileges(ctypes.Structure):
    _fields_ = [("count", wintypes.DWORD), ("luid", Luid), ("attributes", wintypes.DWORD)]

user32 = ctypes.WinDLL("user32")
kernel32 = ctypes.WinDLL("kernel32")
advapi32 = ctypes.WinDLL("advapi32")

user32.GetAsyncKeyState.argtypes = [ctypes.c_int]
user32.GetAsyncKeyState.restype = wintypes.SHORT
user32.MapVirtualKeyW.argtypes = [wintypes.UINT, wintypes.UINT]
user32.MapVirtualKeyW.restype = wintypes.UINT
user32.GetKeyNameTextW.argtypes = [wintypes.LONG, wintypes.LPWSTR, ctypes.c_int]
user32.GetKeyNameTextW.restype = ctypes.c_int
user32.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(Input), ctypes.c_int]
user32.SendInput.restype = wintypes.UINT
user32.ExitWindowsEx.argtypes = [wintypes.UINT, wintypes.DWORD]
user32.ExitWindowsEx.restype = wintypes.BOOL
kernel32.GetCurrentProcess.argtypes = []
kernel32.GetCurrentProcess.restype = wintypes.HANDLE
kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
kernel32.CloseHandle.restype = wintypes.BOOL
advapi32.OpenProcessToken.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.POINTER(wintypes.HANDLE),]
advapi32.OpenProcessToken.restype = wintypes.BOOL
advapi32.LookupPrivilegeValueW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR, ctypes.POINTER(Luid),]
advapi32.LookupPrivilegeValueW.restype = wintypes.BOOL
advapi32.AdjustTokenPrivileges.argtypes = [
    wintypes.HANDLE, wintypes.BOOL, ctypes.POINTER(TokenPrivileges),
    wintypes.DWORD, ctypes.POINTER(TokenPrivileges), ctypes.POINTER(wintypes.DWORD),
]
advapi32.AdjustTokenPrivileges.restype = wintypes.BOOL


class Core:
    downFlags = (0x0002, 0x0020, 0x0008)
    upFlags = (0x0004, 0x0040, 0x0010)

    def __init__(self):
        self.clickType = 0
        self.gapMs = 100
        self.powerOffMins = 0
        self.hotKey = 0
        self.isSettingHotKey = False
        self.hotKeyName = "None"
        self.isRunning = False
        self.keyDown = [False] * 256
        self.started = self.nextClick = 0.0

    def Update(self):
        isDown = [False] * 256
        for key in range(8, 256):
            isDown[key] = self.IsKeyboardKey(key) and (user32.GetAsyncKeyState(key) & 0x8000) != 0

        if self.isSettingHotKey:
            if isDown[27]:
                self.isSettingHotKey = False
            else:
                for key in range(8, 256):
                    if isDown[key] and not self.keyDown[key]:
                        self.SetHotKey(key)
                        self.isSettingHotKey = False
                        break
        elif self.hotKey and isDown[self.hotKey] and not self.keyDown[self.hotKey]:
            self.ToggleClicking()

        self.keyDown = isDown
        if not self.isRunning:
            return

        now = monotonic()
        if self.powerOffMins > 0 and now >= self.started + self.powerOffMins * 60:
            self.isRunning = False
            self.ShutDown()
            return

        if now >= self.nextClick:
            self.Click(self.clickType)
            self.nextClick += self.gapMs / 1000
            if now >= self.nextClick:
                self.nextClick = now + self.gapMs / 1000

    def ToggleHotKeyBinding(self):
        self.isSettingHotKey = not self.isSettingHotKey
        if self.isSettingHotKey:
            self.isRunning = False

    def ToggleClicking(self):
        self.isRunning = not self.isRunning
        if self.isRunning:
            self.started = monotonic()
            self.nextClick = self.started

    def SetHotKey(self, virtualKey):
        self.hotKey = virtualKey
        if 112 <= virtualKey <= 135:
            self.hotKeyName = f"F{virtualKey - 111}"
        elif 96 <= virtualKey <= 105:
            self.hotKeyName = f"Keypad{virtualKey - 96}"
        elif virtualKey == 166:
            self.hotKeyName = "BrowserBack"
        elif virtualKey == 167:
            self.hotKeyName = "BrowserForward"
        else:
            scanCode = user32.MapVirtualKeyW(virtualKey, 4)
            keyData = (scanCode & 0xFF) << 16
            if scanCode & 0xFF00 == 0xE000:
                keyData |= 1 << 24
            name = ctypes.create_unicode_buffer(64)
            self.hotKeyName = (
                name.value
                if user32.GetKeyNameTextW(keyData, name, len(name))
                else f"VK 0x{virtualKey:02X}"
            )

    @staticmethod
    def IsKeyboardKey(key):
        if key <= 6 or key in (16, 17, 18, 229, 231):
            return False
        return user32.MapVirtualKeyW(key, 0) != 0 or key in (166, 167)

    @staticmethod
    def Click(clickType):
        inputs = (Input * 2)(Input(mouse=MouseInput(flags=Core.downFlags[clickType])), Input(mouse=MouseInput(flags=Core.upFlags[clickType])))
        user32.SendInput(2, inputs, ctypes.sizeof(Input))

    @staticmethod
    def ShutDown():
        token = wintypes.HANDLE()
        advapi32.OpenProcessToken(kernel32.GetCurrentProcess(), 0x0020 | 0x0008, ctypes.byref(token))
        privileges = TokenPrivileges(count=1, attributes=2)
        advapi32.LookupPrivilegeValueW(None, "SeShutdownPrivilege", ctypes.byref(privileges.luid))
        previous = TokenPrivileges()
        previousSize = wintypes.DWORD()
        advapi32.AdjustTokenPrivileges(token, False, ctypes.byref(privileges), ctypes.sizeof(previous), ctypes.byref(previous), ctypes.byref(previousSize))
        user32.ExitWindowsEx(0x00000008, 0x00040000 | 0x80000000)
        advapi32.AdjustTokenPrivileges(token, False, ctypes.byref(previous), 0, None, None)
        kernel32.CloseHandle(token)