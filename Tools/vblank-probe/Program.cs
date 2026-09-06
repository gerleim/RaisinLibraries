using System.Diagnostics;
using System.Runtime.InteropServices;

// Reports each display's vblank period, by waiting on it.
//
// Why this exists: WPF composes on one clock derived from the primary display, so an app that
// wants to pace repaints to the panel a window is actually on cannot get the timing from WPF.
// Pacing to the panel's *rate* alone is not enough either - without the phase, a repaint issued
// once per refresh period lands anywhere within it and drifts. See
// design/WPF Presentation Timing.md.
//
// Two candidates were tried. DwmGetCompositionTimingInfo fails with 0x88980090 even given a real
// window handle. D3DKMTWaitForVerticalBlankEvent works, and is per video-present-source, so it can
// answer for a chosen output once the adapter and source are identified for it.
//
// Expect exact answers on a quiet machine, and degradation under load that is worst at the highest
// refresh rate. Run once with build processes still busy and a 280 Hz panel read 12.6 ms - about
// 3.5 refreshes - because the waiting thread was missing vblanks and timing multiples; the 60, 100
// and 144 Hz panels in the same run were unaffected, having 10-17 ms of slack to absorb the same
// jitter. Quiet, the 280 Hz panel reads 3.565 ms repeatably.
//
// So a reading far from a panel's known rate means the machine was busy, not that the panel
// changed. Cross-check against Displays.RefreshHz, which reads the mode rather than timing it.
//
// Usage:  dotnet run --project Tools/vblank-probe [\.\DISPLAY1 ...]
//         with no arguments it probes DISPLAY6 through DISPLAY9.

internal static class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_TIMING_INFO
    {
        public int cbSize;
        public int rateRefreshNum, rateRefreshDen;
        public long qpcRefreshPeriod;
        public int rateComposeNum, rateComposeDen;
        public ulong qpcVBlank;
        public ulong cRefresh;
        public uint cDXRefresh;
        public ulong qpcCompose;
        public ulong cFrame;
        public uint cDXPresent;
        public ulong cRefreshFrame;
        public ulong cFrameSubmitted;
        public uint cDXPresentSubmitted;
        public ulong cFrameConfirmed;
        public uint cDXPresentConfirmed;
        public ulong cRefreshConfirmed;
        public uint cDXRefreshConfirmed;
        public ulong cFramesLate, cFramesOutstanding, cFrameDisplayed;
        public ulong qpcFrameDisplayed, cRefreshFrameDisplayed;
        public ulong cFrameComplete, qpcFrameComplete;
        public ulong cFramePending, qpcFramePending;
        public ulong cFramesDisplayed, cFramesComplete, cFramesPending;
        public ulong cFramesAvailable, cFramesDropped, cFramesMissed;
        public ulong cRefreshNextDisplayed, cRefreshNextPresented;
        public ulong cRefreshesDisplayed, cRefreshesPresented;
        public ulong cRefreshStarted;
        public ulong cPixelsReceived, cPixelsDrawn, cBuffersEmpty;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref DWM_TIMING_INFO info);

    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_OPENADAPTERFROMHDC
    {
        public IntPtr hDc;
        public uint hAdapter;
        // LUID is { DWORD LowPart; LONG HighPart; } - two 4-byte fields, so the struct aligns to 4.
        // Declaring it as a single long forces 8-byte alignment, inserts 4 bytes of padding after
        // hAdapter, and pushes VidPnSourceId off the end - which is why every display reported
        // source 0 on the first attempt.
        public uint AdapterLuidLow;
        public int AdapterLuidHigh;
        public uint VidPnSourceId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_CREATEDEVICE
    {
        public IntPtr hAdapter;          // union with pAdapter, so pointer-sized
        public uint Flags;
        public uint hDevice;
        public IntPtr pCommandBuffer;
        public uint CommandBufferSize;
        public IntPtr pAllocationList;
        public uint AllocationListSize;
        public IntPtr pPatchLocationList;
        public uint PatchLocationListSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_DESTROYDEVICE { public uint hDevice; }

    [DllImport("gdi32.dll")] private static extern int D3DKMTCreateDevice(ref D3DKMT_CREATEDEVICE d);
    [DllImport("gdi32.dll")] private static extern int D3DKMTDestroyDevice(ref D3DKMT_DESTROYDEVICE d);

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_WAITFORVERTICALBLANKEVENT
    {
        public uint hAdapter;
        public uint hDevice;
        public uint VidPnSourceId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3DKMT_CLOSEADAPTER { public uint hAdapter; }

    [DllImport("gdi32.dll")] private static extern int D3DKMTOpenAdapterFromHdc(ref D3DKMT_OPENADAPTERFROMHDC a);
    [DllImport("gdi32.dll")] private static extern int D3DKMTWaitForVerticalBlankEvent(ref D3DKMT_WAITFORVERTICALBLANKEVENT w);
    [DllImport("gdi32.dll")] private static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER c);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDCW(string? driver, string device, string? port, IntPtr devMode);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);

    private static double Ms(long qpc) => qpc * 1000.0 / Stopwatch.Frequency;

    private static void Main(string[] args)
    {
        Console.WriteLine($"QPC frequency: {Stopwatch.Frequency:N0} Hz");
        Console.WriteLine();

        var info = new DWM_TIMING_INFO { cbSize = Marshal.SizeOf<DWM_TIMING_INFO>() };
        int hr = DwmGetCompositionTimingInfo(GetDesktopWindow(), ref info);
        if (hr != 0)
        {
            Console.WriteLine($"DwmGetCompositionTimingInfo failed: 0x{hr:X8}");
        }
        else
        {
            double refreshHz = info.rateRefreshDen > 0 ? (double)info.rateRefreshNum / info.rateRefreshDen : 0;
            Console.WriteLine("DwmGetCompositionTimingInfo (documented as desktop-wide):");
            Console.WriteLine($"  refresh rate     : {refreshHz:F2} Hz");
            Console.WriteLine($"  qpcRefreshPeriod : {Ms(info.qpcRefreshPeriod):F4} ms  => {1000.0 / Ms(info.qpcRefreshPeriod):F1} Hz");

            ulong vb0 = info.qpcVBlank, r0 = info.cRefresh;
            Thread.Sleep(500);
            var info2 = new DWM_TIMING_INFO { cbSize = Marshal.SizeOf<DWM_TIMING_INFO>() };
            DwmGetCompositionTimingInfo(GetDesktopWindow(), ref info2);
            double advanced = Ms((long)(info2.qpcVBlank - vb0));
            ulong refreshes = info2.cRefresh - r0;
            Console.WriteLine($"  qpcVBlank advanced {advanced:F1} ms over a 500 ms sleep " +
                              $"({(Math.Abs(advanced - 500) < 40 ? "live phase" : "NOT tracking wall clock")})");
            Console.WriteLine($"  {refreshes} refreshes counted in 500 ms => {refreshes * 2.0:F0} Hz");
        }

        Console.WriteLine();
        Console.WriteLine("D3DKMTWaitForVerticalBlankEvent, per display device:");
        string[] devices = args.Length > 0 ? args : new[] { @"\\.\DISPLAY6", @"\\.\DISPLAY7", @"\\.\DISPLAY8", @"\\.\DISPLAY9" };

        foreach (var device in devices)
        {
            IntPtr hdc = CreateDCW(null, device, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero) { Console.WriteLine($"  {device}: CreateDC failed"); continue; }

            var open = new D3DKMT_OPENADAPTERFROMHDC { hDc = hdc };
            int r = D3DKMTOpenAdapterFromHdc(ref open);
            if (r != 0)
            {
                Console.WriteLine($"  {device}: OpenAdapterFromHdc failed 0x{r:X8}");
                DeleteDC(hdc);
                continue;
            }

            // The wait needs a real device handle; passing 0 is not valid and produced numbers
            // belonging to nothing on the first attempt.
            var dev = new D3DKMT_CREATEDEVICE { hAdapter = (IntPtr)open.hAdapter };
            int dr = D3DKMTCreateDevice(ref dev);
            if (dr != 0)
            {
                Console.WriteLine($"  {device}: CreateDevice failed 0x{dr:X8}");
                var c0 = new D3DKMT_CLOSEADAPTER { hAdapter = open.hAdapter };
                D3DKMTCloseAdapter(ref c0);
                DeleteDC(hdc);
                continue;
            }

            var wait = new D3DKMT_WAITFORVERTICALBLANKEVENT
            {
                hAdapter = open.hAdapter,
                hDevice = dev.hDevice,
                VidPnSourceId = open.VidPnSourceId
            };

            var gaps = new List<double>();
            long last = 0;
            bool ok = true;

            for (int i = 0; i < 60; i++)
            {
                r = D3DKMTWaitForVerticalBlankEvent(ref wait);
                if (r != 0)
                {
                    Console.WriteLine($"  {device}: WaitForVerticalBlankEvent failed 0x{r:X8} (source {open.VidPnSourceId})");
                    ok = false;
                    break;
                }
                long now = Stopwatch.GetTimestamp();
                if (last != 0) gaps.Add(Ms(now - last));
                last = now;
            }

            if (ok && gaps.Count > 5)
            {
                gaps.Sort();
                double med = gaps[gaps.Count / 2];
                Console.WriteLine($"  {device}: source {open.VidPnSourceId}  interval median {med:F3} ms => {1000.0 / med:F1} Hz  " +
                                  $"(min {gaps[0]:F3}, max {gaps[^1]:F3}, n={gaps.Count})");
            }

            var destroy = new D3DKMT_DESTROYDEVICE { hDevice = dev.hDevice };
            D3DKMTDestroyDevice(ref destroy);
            var close = new D3DKMT_CLOSEADAPTER { hAdapter = open.hAdapter };
            D3DKMTCloseAdapter(ref close);
            DeleteDC(hdc);
        }
    }
}
