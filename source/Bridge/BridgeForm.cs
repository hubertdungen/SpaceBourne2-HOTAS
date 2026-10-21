using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SB2Hotas;

internal sealed class BridgeForm : Form
{
    const string GameProcess="SB2-Win64-Shipping";
    readonly string profilePath=Data.PathOf("profile.json");
    Settings settings;
    readonly List<Device> devices=[];
    readonly DataGridView grid=new() { Dock=DockStyle.Fill, AllowUserToAddRows=false, AllowUserToDeleteRows=false, RowHeadersVisible=false, AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill, SelectionMode=DataGridViewSelectionMode.FullRowSelect, MultiSelect=false };
    readonly TextBox monitor=new() { Multiline=true, ReadOnly=true, Dock=DockStyle.Fill, ScrollBars=ScrollBars.Vertical, Font=new Font("Consolas",9) };
    readonly Label status=new() { AutoSize=true, MaximumSize=new Size(1000,0), ForeColor=Color.DarkBlue };
    readonly Label headLabel=new() { AutoSize=true };
    readonly DataGridView buttonGrid=new() { Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false };
    readonly ButtonOutput buttonOutput=new();
    int? learningButton;
    readonly HashSet<string> initialButtons=[];
    readonly CheckBox headCheck=new() { Text="Head tracking (OpenTrack) in the cockpit", AutoSize=true };
    readonly CheckBox flightCheck=new() { Text="Flight axes", AutoSize=true, Checked=true };
    readonly CheckBox forwardOnly=new() { Text="Throttle forward only (0…100%)", AutoSize=true };
    readonly CheckBox spanCheck=new() { Text="Stretch the game over all my monitors", AutoSize=true };
    readonly CheckBox fovCheck=new() { Text="Wider view on wide screens (Hor+ field of view: the side screens show more, nothing is stretched)", AutoSize=true };
    readonly CheckBox hudCheck=new() { Text="Keep the HUD and menus on the centre monitor", AutoSize=true };
    readonly Label screenLabel=new() { AutoSize=true, MaximumSize=new Size(1000,0), Padding=new Padding(0,10,0,0) };
    readonly Screens screens=new();
    readonly LinkLabel updateLink=new() { AutoSize=true, Visible=false, Padding=new Padding(8,6,0,0), Font=new Font("Segoe UI",9,FontStyle.Bold) };
    // Fixed on purpose: the update check only reads a version number, it never decides where the link goes.
    const string ReleasesUrl="https://github.com/hubertdungen/SpaceBourne2-HOTAS/releases/latest";
    bool reverseToggled;
    readonly Dictionary<ButtonBinding,bool> lastPressed=[];
    readonly Dictionary<ButtonBinding,(int Pending,long Down,long Gap)> taps=[];
    readonly double[] keyboard=new double[6];
    // Game default keys for the movements the keyboard fills in while the mod flies: index -> (plus, minus).
    static readonly Dictionary<int,(int Plus,int Minus)> KeyboardKeys=new() { [1]=(0x45,0x51), [3]=(0x57,0x53), [4]=(0x44,0x41), [5]=(0x52,0x56) };   // roll E/Q, throttle W/S, strafe D/A, lift R/V
    readonly NumericUpDown turnRate=new() { Minimum=10, Maximum=120, Width=60 };
    readonly Button armButton=new() { Text="ARM", AutoSize=true };
    readonly Button calibrate=new() { Text="Calibrate axes", AutoSize=true };
    readonly System.Windows.Forms.Timer timer=new() { Interval=16 };
    readonly object poseLock=new();
    readonly CancellationTokenSource cancel=new();
    UdpClient? udp;
    double[] pose=new double[6], zero=new double[6];
    long poseTime, sequence;
    string? frameTrouble;   // why frames don't reach the mod (Data.Replace), shown at the top of the window
    bool armed, calibration, closing, sdl;
    string? learning;
    long learnDeadline;
    readonly Dictionary<string,short[]> learnStart=[];
    readonly Dictionary<string,(double Min,double Max)> ranges=[];
    int ticks;
    public BridgeForm()
    {
        Directory.CreateDirectory(Data.Dir);
        settings=Settings.Load(profilePath);
        Text=$"SpaceBourne 2 HOTAS {Application.ProductVersion} — joystick, pedals & head tracking";
        try { Icon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }   // the exe's own icon, for the title bar and taskbar
        Size=new Size(1100,770); MinimumSize=new Size(1000,700); StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",10);
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(16), RowCount=6, ColumnCount=1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,250));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text="Assign the movements you want on your controllers: select one, click Detect axis and move only that control. "
            +"Movements you leave unassigned stay on keyboard and mouse.\nThen click Calibrate axes, move every assigned axis through its full range, "
            +"return sticks and pedals to centre and click Finish.", AutoSize=true, MaximumSize=new Size(960,0), Padding=new Padding(0,0,0,10) },0,0);
        foreach(var name in new[]{"Movement","Device","Axis","Invert","Deadzone","Curve","Calibrated"})
        {
            DataGridViewColumn col=name=="Invert" ? new DataGridViewCheckBoxColumn() : new DataGridViewTextBoxColumn();
            col.Name=name; col.HeaderText=name; col.ReadOnly=name is "Movement" or "Device" or "Axis" or "Calibrated";
            if(name is "Deadzone" or "Curve") col.ValueType=typeof(double);
            if(name=="Device") col.FillWeight=220;
            grid.Columns.Add(col);
        }
        foreach(var m in Settings.AllMovements) grid.Rows.Add(m,"","",false,0.0,0.0,"");
        grid.DataError+=(_,e)=> { e.ThrowException=false; status.Text="Enter a valid number."; };
        layout.Controls.Add(grid,0,1);
        void AddButton(FlowLayoutPanel panel,string label,Action action) { var b=new Button {Text=label,AutoSize=true}; b.Click+=(_,_)=>Safe(action); panel.Controls.Add(b); }
        var actions=new FlowLayoutPanel { AutoSize=true, Dock=DockStyle.Fill };
        AddButton(actions,"Scan devices",RefreshDevices);
        AddButton(actions,"Detect axis",Learn);
        calibrate.Click+=(_,_)=>Safe(Calibrate); actions.Controls.Add(calibrate);
        AddButton(actions,"Unassign",()=> { Disarm(); var key=Selected(); settings.Axes[key]=AxisBinding.For(key); UpdateRows(); Save(); });
        AddButton(actions,"Save",Save);
        AddButton(actions,"Import profile…",ImportProfile);
        AddButton(actions,"Export profile…",ExportProfile);
        AddButton(actions,"Install mod…",InstallMod);
        AddButton(actions,"Uninstall mod…",UninstallMod);
        AddButton(actions,"▶ Play SpaceBourne 2",PlayGame);
        layout.Controls.Add(actions,0,2);
        var tabs=new TabControl { Dock=DockStyle.Fill };
        var axisTab=new TabPage("Axis monitor"); axisTab.Controls.Add(monitor); tabs.TabPages.Add(axisTab);
        var buttonTab=new TabPage("Buttons");
        buttonGrid.Columns.Add("name","Name"); buttonGrid.Columns.Add("device","Device"); buttonGrid.Columns.Add("input","Input");
        buttonGrid.Columns[1].ReadOnly=true; buttonGrid.Columns[2].ReadOnly=true;
        // Row i is settings.Buttons[i] everywhere, so the rows must never be re-sorted by a header click.
        foreach(DataGridViewColumn c in buttonGrid.Columns) c.SortMode=DataGridViewColumnSortMode.NotSortable;
        var outputCol=new DataGridViewComboBoxColumn {Name="output",HeaderText="Game key"}; outputCol.Items.AddRange(ButtonOutput.Options); buttonGrid.Columns.Add(outputCol);
        var modeCol=new DataGridViewComboBoxColumn {Name="mode",HeaderText="Mode"}; modeCol.Items.AddRange(ButtonBinding.Modes); buttonGrid.Columns.Add(modeCol);
        foreach(var b in settings.Buttons) buttonGrid.Rows.Add(b.Name,"","",b.Output,b.Mode);
        buttonGrid.DataError+=(_,e)=> e.ThrowException=false;
        var buttonActions=new FlowLayoutPanel { AutoSize=true, Dock=DockStyle.Bottom };
        AddButton(buttonActions,"Detect input for selected row",LearnButton);
        AddButton(buttonActions,"Add row",AddButtonRow);
        AddButton(buttonActions,"Remove row",RemoveButtonRow);
        AddButton(buttonActions,"Clear input",ClearButtonRow);
        AddButton(buttonActions,"Reset all…",ResetButtons);
        buttonTab.Controls.Add(buttonGrid); buttonTab.Controls.Add(buttonActions); tabs.TabPages.Add(buttonTab);
        tabs.TabPages.Add(ScreenTab());
        layout.Controls.Add(tabs,0,3);
        var bottom=new FlowLayoutPanel { AutoSize=true, Dock=DockStyle.Fill };
        headCheck.Checked=settings.HeadEnabled;
        turnRate.Value=(decimal)settings.TurnRate;
        bottom.Controls.Add(flightCheck); bottom.Controls.Add(headCheck);
        forwardOnly.Checked=settings.Axes["Throttle"].Unipolar;
        forwardOnly.CheckedChanged+=(_,_)=>Safe(()=>
        {
            var t=settings.Axes["Throttle"];
            if(t.Unipolar==forwardOnly.Checked) return;
            Disarm(); t.Unipolar=forwardOnly.Checked; t.Confirmed=false; UpdateRows(); Save();
            status.Text=forwardOnly.Checked ? "Throttle is now forward only (0…100%). Click Calibrate axes."
                : "Throttle now covers reverse too (−100…+100%, middle = stop). Click Calibrate axes and finish with the throttle in the middle.";
        });
        bottom.Controls.Add(forwardOnly);
        var center=new Button { Text="Recenter head",AutoSize=true }; center.Click+=(_,_)=>CenterHead(); bottom.Controls.Add(center);
        bottom.Controls.Add(new Label { Text="Turn rate °/s", AutoSize=true, Padding=new Padding(8,6,0,0) }); bottom.Controls.Add(turnRate);
        armButton.Click+=(_,_)=>Safe(ToggleArm); bottom.Controls.Add(armButton);
        bottom.Controls.Add(headLabel);
        updateLink.LinkClicked+=(_,_)=>Safe(()=>Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute=true }));
        bottom.Controls.Add(updateLink); layout.Controls.Add(bottom,0,4);
        _=CheckForUpdate();
        layout.Controls.Add(status,0,5); Controls.Add(layout);
        // The hint must be set before SDL_Init: the bridge reads the controllers while the game has focus.
        try
        {
            Sdl.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS","1");
            if(Sdl.SDL_Init(0x200 | 0x4000)!=0) throw new IOException("Could not start the controller reader (SDL2.dll).");
            sdl=true;
            RefreshDevices();
        }
        catch(DllNotFoundException)
        {
            // Game not found by Steam and no SDL2.dll here yet: keep the window usable so Install mod can fix it.
            UpdateRows(); RefreshButtonRows();
            status.Text=NoSdl;
        }
        try { udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,settings.UdpPort)); _=ReceivePose(); }
        catch(SocketException e) { status.Text=$"Head tracking unavailable: UDP port {settings.UdpPort} is in use. {e.Message}"; }
        UpdateScreens();
        timer.Tick+=(_,_)=>Safe(Poll); timer.Start();
        FormClosing+=(_,_)=>CloseBridge();
    }
    void Safe(Action action)
    {
        try { action(); }
        catch(Exception e) { Disarm(); status.Text="Disarmed: "+e.Message; }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        bool stop=Native.RegisterHotKey(Handle,1,0x4003,0x7B);
        bool center=Native.RegisterHotKey(Handle,2,0x4003,0x7A);
        if(!stop || !center) status.Text="Another app owns Ctrl+Alt+F11/F12. Use the DISARM button in this window.";
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x312) { if(m.WParam==1) Disarm(); else if(m.WParam==2) CenterHead(); }
        base.WndProc(ref m);
    }
    void CenterHead() { lock(poseLock) zero=(double[])pose.Clone(); }
    TabPage ScreenTab()
    {
        var page=new TabPage("Triple screen");
        var panel=new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(8) };
        panel.Controls.Add(new Label { AutoSize=true, MaximumSize=new Size(1000,0), Padding=new Padding(0,0,0,8), Text=
            "Play on two, three or more monitors as one wide screen, without NVIDIA Surround or AMD Eyefinity. "
            +"In SpaceBourne 2, set Display Mode to Windowed Fullscreen, then tick the first box. This works with or without ARM, "
            +"and the game goes back to one monitor when you untick it." });
        spanCheck.Checked=settings.SpanScreens; fovCheck.Checked=settings.WideFov; hudCheck.Checked=settings.CentreHud;
        foreach(var box in new[]{spanCheck,fovCheck,hudCheck})
        {
            box.CheckedChanged+=(_,_)=>Safe(()=>
            {
                // Stretching wants the other two as well; they stay separately untickable.
                if(box==spanCheck && spanCheck.Checked && !fovCheck.Checked && !hudCheck.Checked) { fovCheck.Checked=true; hudCheck.Checked=true; }
                Save(); UpdateScreens();
                status.Text="Triple screen: "+(spanCheck.Checked ? "on" : "off")+", wide view "+(fovCheck.Checked ? "on" : "off")
                    +", HUD on the centre monitor "+(hudCheck.Checked ? "on" : "off")+". Saved.";
            });
            panel.Controls.Add(box);
        }
        panel.Controls.Add(screenLabel);
        page.Controls.Add(panel);
        return page;
    }
    // Steam copies start through Steam, so the launch options and the overlay still apply; another copy (GOG)
    // starts from the folder Install mod used, or one the player picks now.
    void PlayGame()
    {
        if(Process.GetProcessesByName(GameProcess).Length>0) { status.Text="SpaceBourne 2 is already running."; return; }
        var steam=GameInstall.FindGame();
        var folder=settings.GameFolder!="" && GameInstall.IsGame(settings.GameFolder) ? settings.GameFolder : steam;
        if(folder==null)
        {
            using var dialog=new FolderBrowserDialog { Description="Select the SpaceBourne 2 folder (the one that contains SB2)", UseDescriptionForTitle=true };
            if(dialog.ShowDialog(this)!=DialogResult.OK) return;
            if(!GameInstall.IsGame(dialog.SelectedPath)) throw new IOException(@"That folder is not SpaceBourne 2 (there is no SB2\Binaries\Win64\SB2-Win64-Shipping.exe inside).");
            folder=dialog.SelectedPath; settings.GameFolder=folder; Save();
        }
        if(steam!=null && string.Equals(Path.GetFullPath(folder).TrimEnd('\\'),Path.GetFullPath(steam).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo("steam://rungameid/1646850") { UseShellExecute=true });
        else
        {
            var exe=Path.Combine(folder,"SpaceBourne2.exe");
            if(!File.Exists(exe)) exe=Path.Combine(GameInstall.Win64(folder),"SB2-Win64-Shipping.exe");
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute=true, WorkingDirectory=Path.GetDirectoryName(exe)! });
        }
        status.Text="Starting SpaceBourne 2… Get in your ship, then click ARM (if it isn't armed already).";
    }
    // Once per start: asks GitHub for the latest release and shows a link when it is newer than this one.
    async Task CheckForUpdate()
    {
        try
        {
            using var http=new HttpClient { Timeout=TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SB2-HOTAS/"+Application.ProductVersion);
            var json=await http.GetStringAsync("https://api.github.com/repos/hubertdungen/SpaceBourne2-HOTAS/releases/latest");
            using var doc=System.Text.Json.JsonDocument.Parse(json);
            var tag=doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v','V');
            if(tag is null || !Version.TryParse(tag,out var latest) || !Version.TryParse(Application.ProductVersion,out var mine) || latest<=mine) return;
            updateLink.Text=$"SB2 HOTAS {tag} is out: click here to download it";
            updateLink.Visible=true;
        }
        catch { }   // offline or GitHub unreachable: no news is fine
    }
    // A failure here must never disarm flight, so it only shows on the tab.
    void UpdateScreens()
    {
        try { screenLabel.Text=screens.Tick(spanCheck.Checked,fovCheck.Checked,hudCheck.Checked,GameProcess); }
        catch(Exception e) { screenLabel.Text="Triple screen: "+e.Message; }
    }
    void Disarm()
    {
        armed=false; armButton.Text="ARM"; reverseToggled=false;
        try { buttonOutput.Release(); } catch { }
        try { WriteFrame(false,false,new double[6],new double[6],0); } catch { }
    }
    string Selected()=>grid.CurrentRow?.Cells[0].Value?.ToString()??"Pitch";
    void Save()
    {
        grid.EndEdit(); buttonGrid.EndEdit();
        for(int i=0;i<buttonGrid.Rows.Count;i++)
        {
            settings.Buttons[i].Name=buttonGrid.Rows[i].Cells[0].Value?.ToString()??"Button";
            settings.Buttons[i].Output=buttonGrid.Rows[i].Cells[3].Value?.ToString()??"None";
            settings.Buttons[i].Mode=buttonGrid.Rows[i].Cells[4].Value?.ToString()??"Hold";
        }
        foreach(DataGridViewRow row in grid.Rows)
        {
            var b=settings.Axes[row.Cells[0].Value!.ToString()!];
            b.Invert=Convert.ToBoolean(row.Cells[3].Value);
            // A cleared cell holds DBNull: keep the previous value instead of failing every Save.
            b.Deadzone=Math.Clamp(row.Cells[4].Value is double dz ? dz : b.Deadzone,0,.4);
            b.Curve=Math.Clamp(row.Cells[5].Value is double cv ? cv : b.Curve,.5,3);
        }
        settings.HeadEnabled=headCheck.Checked;
        settings.TurnRate=(double)turnRate.Value;
        settings.SpanScreens=spanCheck.Checked; settings.WideFov=fovCheck.Checked; settings.CentreHud=hudCheck.Checked;
        settings.Save(profilePath);
    }
    void UpdateRows()
    {
        foreach(DataGridViewRow row in grid.Rows)
        {
            var b=settings.Axes[row.Cells[0].Value!.ToString()!];
            bool assigned=b.Device!="";
            row.Cells[1].Value=DeviceLabel(b.Device); row.Cells[2].Value=assigned ? b.Axis.ToString() : "";
            row.Cells[3].Value=b.Invert; row.Cells[4].Value=b.Deadzone; row.Cells[5].Value=b.Curve;
            row.Cells[6].Value=!assigned ? "—" : b.Confirmed ? "Yes" : "No";
        }
        forwardOnly.Checked=settings.Axes["Throttle"].Unipolar;   // Unassign and imports can change it
    }
    void RefreshButtonRows()
    {
        for(int i=0;i<buttonGrid.Rows.Count;i++)
        {
            var b=settings.Buttons[i]; var d=FindDevice(b.Device);
            buttonGrid.Rows[i].Cells[1].Value=DeviceLabel(b.Device);
            buttonGrid.Rows[i].Cells[2].Value=b.Button<0 ? "" : d!=null && b.Button<d.Buttons ? d.InputName(b.Button) : $"Input {b.Button+1}";
        }
    }
    string DeviceLabel(string key)
    {
        if(key=="") return "";
        var d=FindDevice(key);
        if(d!=null) return d.Label;
        var parts=key.Split(':');
        return (parts.Length>=3 ? parts[2] : key)+" (not connected)";
    }
    void AddButtonRow()
    {
        Save();
        var b=new ButtonBinding { Name=$"Button {settings.Buttons.Count+1}" };
        settings.Buttons.Add(b); buttonGrid.Rows.Add(b.Name,"","",b.Output,b.Mode);
        buttonGrid.CurrentCell=buttonGrid.Rows[buttonGrid.Rows.Count-1].Cells[0];
        Save();
    }
    // Unbinds the selected row but keeps its name and game key, ready for Detect input.
    void ClearButtonRow()
    {
        if(buttonGrid.CurrentRow==null) throw new IOException("Select a row first.");
        Disarm(); Save(); learningButton=null;
        var b=settings.Buttons[buttonGrid.CurrentRow.Index]; b.Device=""; b.Button=-1;
        RefreshButtonRows(); Save();
        status.Text=$"\"{b.Name}\" has no input now. Click Detect input for selected row to give it one.";
    }
    // Back to the game's 26 ship actions with their default keys, which also brings back removed rows.
    void ResetButtons()
    {
        if(MessageBox.Show(this,"Replace every button row with the game's 26 ship actions and their default keys?\n\nAll your button bindings will be cleared.",
            "SB2 HOTAS",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK) return;
        Disarm(); learningButton=null;
        settings.Buttons=Settings.GameActions();
        buttonGrid.Rows.Clear();
        foreach(var b in settings.Buttons) buttonGrid.Rows.Add(b.Name,"","",b.Output,b.Mode);
        RefreshButtonRows(); settings.Save(profilePath);
        status.Text="Button rows reset to the game's ship actions. Use Detect input on the ones you want.";
    }
    void RemoveButtonRow()
    {
        if(buttonGrid.CurrentRow==null) return;
        Disarm(); Save(); learningButton=null;
        var i=buttonGrid.CurrentRow.Index;
        settings.Buttons.RemoveAt(i); buttonGrid.Rows.RemoveAt(i);
        Save();
    }
    void RefreshDevices()
    {
        if(!sdl) throw new IOException(NoSdl);
        Disarm(); calibration=false; learning=null; learningButton=null; calibrate.Text="Calibrate axes";
        foreach(var d in devices) d.Dispose(); devices.Clear();
        Sdl.SDL_PumpEvents();
        var opened=new List<Device>();
        for(int i=0;i<Sdl.SDL_NumJoysticks();i++)
        {
            try { opened.Add(new Device(i)); } catch(IOException) {}
        }
        // Identical controllers (HOSAS twins) are numbered by USB port path, not detection order, so "#2" stays the
        // same physical stick across reboots as long as each stick stays in its USB port.
        foreach(var twins in opened.GroupBy(d=>d.Key).Where(g=>g.Count()>1))
        {
            int n=1;
            foreach(var d in twins.OrderBy(d=>d.DevicePath,StringComparer.Ordinal).ToList()) { if(n>1) d.Number(n); n++; }
        }
        devices.AddRange(opened);
        if(AdoptDevices()) settings.Save(profilePath);
        UpdateRows(); RefreshButtonRows();
        status.Text=devices.Count==0 ? "No controllers found. Connect your joystick, throttle or pedals and click Scan devices."
            : $"Found: {string.Join(", ",devices.Select(d=>d.Label))}. Select a movement and click Detect axis.";
    }
    void Learn()
    {
        Disarm(); Save(); learningButton=null;
        if(devices.Count==0) throw new IOException("Connect your controllers and click Scan devices.");
        learning=Selected(); learnDeadline=Environment.TickCount64+10000; learnStart.Clear();
        foreach(var d in devices) learnStart[d.Key]=Enumerable.Range(0,d.Count).Select(d.Raw).ToArray();
        status.Text=$"Move only the control for {learning}. You have 10 seconds.";
    }
    void LearnButton()
    {
        Disarm(); Save(); learning=null;
        if(buttonGrid.CurrentRow==null) throw new IOException("Select a row first, or click Add row.");
        if(devices.Count==0) throw new IOException("Connect your controllers and click Scan devices.");
        learningButton=buttonGrid.CurrentRow.Index;
        initialButtons.Clear();
        foreach(var d in devices.Where(d=>d.Connected))
            for(int i=0;i<d.Buttons;i++) if(d.Pressed(i)) initialButtons.Add(d.Key+"/"+i);
        learnDeadline=Environment.TickCount64+10000;
        status.Text="Press the button or hat direction for this row (10 seconds).";
    }
    void Calibrate()
    {
        Disarm(); Save(); learning=null; learningButton=null;
        if(!calibration)
        {
            ranges.Clear();
            foreach(var pair in settings.Axes)
            {
                var d=Find(pair.Value);
                if(d!=null) ranges[pair.Key]=(d.Raw(pair.Value.Axis),d.Raw(pair.Value.Axis));
            }
            if(ranges.Count==0) throw new IOException("Assign at least one axis first (Detect axis).");
            calibration=true; calibrate.Text="Finish (controls at rest)";
            status.Text="Move every assigned axis through its full range. Then centre sticks and pedals (a forward-only throttle can stay anywhere; a full-range one goes to the middle) and click Finish.";
        }
        else
        {
            calibration=false; calibrate.Text="Calibrate axes";
            var failed=new List<string>();
            foreach(var pair in ranges)
            {
                var b=settings.Axes[pair.Key]; var d=Find(b);
                // An axis left untouched this round keeps its earlier calibration, so axes can be done one at a time.
                if(d!=null && pair.Value.Max-pair.Value.Min>=10000)
                {
                    b.Min=pair.Value.Min; b.Max=pair.Value.Max; b.Center=d.Raw(b.Axis);
                    b.Confirmed=b.Unipolar || (b.Center>b.Min+1000 && b.Center<b.Max-1000);
                }
                if(!b.Confirmed) failed.Add(pair.Key);
            }
            UpdateRows(); Save();
            status.Text=failed.Count==0 ? "Calibration saved. Check the directions in the Axis monitor and tick Invert where needed."
                : $"Not calibrated: {string.Join(", ",failed)}. Move them through their full range, finish with them centred, and try again.";
        }
    }
    // A profile made on another PC (a template, a shared setup, or Windows vs. Proton) may name a controller
    // differently; take the one connected controller with the same vendor and product ("*" = any product).
    bool AdoptDevices()
    {
        bool changed=false;
        string Adopt(string key)
        {
            var p=key.Split(':');
            // Never adopt a numbered twin ("…#2"): with one of two identical sticks unplugged, its bindings would move
            // onto the other one for good.
            if(key=="" || p.Length<2 || FindDevice(key)!=null || System.Text.RegularExpressions.Regex.IsMatch(key,@"#\d+$")) return key;
            var m=devices.Where(d=>d.Connected && d.Key.Split(':') is var k && k[0].Equals(p[0],StringComparison.OrdinalIgnoreCase)
                && (p[1]=="*" || k[1].Equals(p[1],StringComparison.OrdinalIgnoreCase))).ToArray();
            if(m.Length!=1) return key;
            changed=true; return m[0].Key;
        }
        foreach(var b in settings.Axes.Values) b.Device=Adopt(b.Device);
        foreach(var b in settings.Buttons) b.Device=Adopt(b.Device);
        return changed;
    }
    void ImportProfile()
    {
        using var dialog=new OpenFileDialog { Title="Import profile", Filter="SB2 HOTAS profile (*.json)|*.json", InitialDirectory=Path.Combine(AppContext.BaseDirectory,"templates") };
        if(dialog.ShowDialog(this)!=DialogResult.OK) return;
        Save(); Disarm();
        var imported=Settings.Load(dialog.FileName);
        var name=Path.GetFileNameWithoutExtension(dialog.FileName);
        if(Settings.IsFullProfile(dialog.FileName))
        {
            // Screens belong to this PC, not to the controllers: keep the current triple-screen choices.
            imported.SpanScreens=spanCheck.Checked; imported.WideFov=fovCheck.Checked; imported.CentreHud=hudCheck.Checked;
            imported.GameFolder=settings.GameFolder;
            settings=imported;
            buttonGrid.Rows.Clear();
            foreach(var b in settings.Buttons) buttonGrid.Rows.Add(b.Name,"","",b.Output,b.Mode);
            headCheck.Checked=settings.HeadEnabled; turnRate.Value=(decimal)settings.TurnRate;
            forwardOnly.Checked=settings.Axes["Throttle"].Unipolar;
            Rescan(); settings.Save(profilePath);
            status.Text=$"Imported profile \"{name}\".";
            return;
        }
        // A device template only fills the movements it assigns, so a stick, a throttle and pedals can be
        // imported one after another (later imports win, e.g. pedals take yaw from a stick's twist).
        var added=imported.Axes.Where(p=>p.Value.Device!="").Select(p=>p.Key).ToArray();
        foreach(var m in added) settings.Axes[m]=imported.Axes[m];
        Rescan(); settings.Save(profilePath);
        status.Text=$"Added \"{name}\" for {string.Join(", ",added)}. Import your other controllers the same way, then click Calibrate axes.";
    }
    void Rescan() { if(sdl) RefreshDevices(); else { UpdateRows(); RefreshButtonRows(); } }
    const string NoSdl="The controller library (SDL2.dll, which comes with the game) was not found. Click Install mod…, pick your SpaceBourne 2 folder, then restart the bridge.";
    string? PickGame(string action)
    {
        var found=GameInstall.FindGame();
        if(found!=null)
        {
            var answer=MessageBox.Show(this,$"{action} in this SpaceBourne 2 folder?\n\n{found}\n\nYes: this folder.   No: choose another one.","SB2 HOTAS",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(answer==DialogResult.Yes) return found;
            if(answer==DialogResult.Cancel) return null;
        }
        using var dialog=new FolderBrowserDialog { Description="Select the SpaceBourne 2 folder (the one that contains SB2)", UseDescriptionForTitle=true };
        if(dialog.ShowDialog(this)!=DialogResult.OK) return null;
        if(!GameInstall.IsGame(dialog.SelectedPath)) throw new IOException(@"That folder is not SpaceBourne 2 (there is no SB2\Binaries\Win64\SB2-Win64-Shipping.exe inside).");
        return dialog.SelectedPath;
    }
    void InstallMod()
    {
        Disarm();
        var game=PickGame("Install the mod"); if(game==null) return;
        if(Process.GetProcessesByName(GameProcess).Length>0) throw new IOException("Quit SpaceBourne 2 before installing.");
        GameInstall.Install(game,GameInstall.PackageRoot,AppContext.BaseDirectory);
        settings.GameFolder=game; Save();   // the Play button starts this copy
        status.Text=sdl ? $"Mod installed in {game}. Start the game, get in your ship and click ARM." : $"Mod installed in {game}. Restart the bridge to use your controllers.";
    }
    void UninstallMod()
    {
        Disarm();
        var game=PickGame("Remove the mod"); if(game==null) return;
        if(Process.GetProcessesByName(GameProcess).Length>0) throw new IOException("Quit SpaceBourne 2 before removing the mod.");
        status.Text=GameInstall.Uninstall(game);
    }
    void ExportProfile()
    {
        Save();
        using var dialog=new SaveFileDialog { Title="Export profile", Filter="SB2 HOTAS profile (*.json)|*.json", FileName="My SB2 HOTAS profile.json" };
        if(dialog.ShowDialog(this)!=DialogResult.OK) return;
        settings.Save(dialog.FileName);
        status.Text=$"Profile exported to {dialog.FileName}.";
    }
    Device? FindDevice(string key)
    {
        var matches=devices.Where(d=>d.Key==key && d.Connected).ToArray();
        return matches.Length==1 ? matches[0]:null;
    }
    Device? Find(AxisBinding b)
    {
        var d=FindDevice(b.Device);
        return d!=null && b.Axis>=0 && b.Axis<d.Count ? d : null;
    }
    // Any subset of movements is fine (the rest stay on keyboard/mouse), but every assigned one must be usable.
    // Look axes don't count: an unplugged look device mustn't stop flight.
    bool AxesReady()=>Settings.Movements.Any(m=>settings.Axes[m].Device!="")
        && Settings.Movements.Append(Settings.Reverse).Select(m=>settings.Axes[m]).All(b=>b.Device=="" || (b.Confirmed && Find(b)!=null));
    double AxisValue(string movement)
    {
        var b=settings.Axes[movement]; var d=Find(b);
        return b.Confirmed && d!=null ? b.Normalize(d.Raw(b.Axis)) : 0;
    }
    static double Key(int vk)=>(Native.GetAsyncKeyState(vk)&0x8000)!=0 ? 1 : 0;
    // One plain instruction at the top of the monitor, so nobody wonders why ARM does nothing.
    string NextStep()
    {
        if(!sdl) return "Click Install mod… and pick your SpaceBourne 2 folder, then restart the bridge.";
        if(devices.Count==0) return "Connect your controllers and click Scan devices.";
        if(calibration) return "Move every assigned axis end to end, centre sticks and pedals, then click Finish.";
        if(!Settings.Movements.Any(m=>settings.Axes[m].Device!=""))
            return "Assign your axes: Import profile… (one template per controller) or select a movement and click Detect axis.";
        var pending=settings.Axes.Where(p=>p.Value.Device!="" && (!p.Value.Confirmed || Find(p.Value)==null)).Select(p=>p.Key).ToArray();
        if(pending.Length>0) return $"Click Calibrate axes. Not calibrated yet: {string.Join(", ",pending)}.";
        if(!armed) return "Ready. Click ARM, then switch to the game and get in your ship.";
        return "Armed. Fly! Ctrl+Alt+F12 disarms.";
    }
    bool LookAssigned()=>Settings.LookMovements.Any(m=>settings.Axes[m].Confirmed && Find(settings.Axes[m])!=null);
    void ToggleArm()
    {
        if(armed) {Disarm(); return;}
        Save();
        if(calibration || learning!=null || learningButton!=null) throw new IOException("Finish calibrating or detecting before arming.");
        if(!flightCheck.Checked && !headCheck.Checked && !LookAssigned() && !settings.Buttons.Any(b=>b.Button>=0 && b.Output!="None"))
            throw new IOException("Nothing to do: tick Flight axes or head tracking, or bind a button.");
        // Arm whatever is ready: head tracking and buttons work even before the flight axes are calibrated.
        armed=true; armButton.Text="DISARM";
        if(flightCheck.Checked && !AxesReady()) status.Text="Armed for head tracking and buttons only. Calibrate your axes to fly with them.";
    }
    bool GameFocused()
    {
        try
        {
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out var pid);
            using var p=Process.GetProcessById((int)pid);
            return p.ProcessName.Equals(GameProcess,StringComparison.OrdinalIgnoreCase);
        } catch {return false;}
    }
    async Task ReceivePose()
    {
        try
        {
            while(!cancel.IsCancellationRequested)
            {
                var data=await udp!.ReceiveAsync(cancel.Token);
                if(!IPAddress.IsLoopback(data.RemoteEndPoint.Address)) continue;
                var v=HeadPacket.Parse(data.Buffer); if(v==null) continue;
                lock(poseLock) {pose=v; poseTime=Environment.TickCount64;}
            }
        }
        catch(OperationCanceledException) {}
        catch(ObjectDisposedException) {}
        catch(SocketException) {lock(poseLock) poseTime=0;}
    }
    void Poll()
    {
        if(closing) return;
        if(ticks%60==0) UpdateScreens();   // about once a second
        if(sdl) { Sdl.SDL_PumpEvents(); Sdl.SDL_JoystickUpdate(); }   // without SDL there are no devices, but head tracking still runs
        var lines=new List<string>();
        foreach(var d in devices)
        {
            if(!d.Connected) continue;
            var raw=Enumerable.Range(0,d.Count).Select(d.Raw).ToArray();
            lines.Add(d.Label+"  "+string.Join("  ",raw.Select((v,i)=>$"{i}: {v,6}")));
            var pressed=Enumerable.Range(0,d.RealInputs).Where(d.Pressed).Select(d.InputName).ToArray();
            if(pressed.Length>0) lines.Add("    pressed: "+string.Join(", ",pressed));
            if(learning!=null && learnStart.TryGetValue(d.Key,out var before))
            {
                var best=Enumerable.Range(0,d.Count).OrderByDescending(i=>Math.Abs(raw[i]-before[i])).FirstOrDefault();
                if(d.Count>0 && Math.Abs(raw[best]-before[best])>9000)
                {
                    var b=settings.Axes[learning]; b.Device=d.Key; b.Axis=best; b.Confirmed=false;
                    status.Text=$"{learning} → {d.Label}, axis {best}. Now calibrate.";
                    learning=null; UpdateRows(); Save();
                }
            }
        }
        if(lines.Count==0) lines.Add("No controllers connected. Plug them in and click Scan devices.");
        if(learning!=null && Environment.TickCount64>learnDeadline) {learning=null; status.Text="No movement detected. Try again.";}
        if(learningButton!=null)
        {
            foreach(var d in devices.Where(d=>d.Connected))
                for(int i=0;i<d.Buttons && learningButton!=null;i++)
                {
                    if(!d.Pressed(i) || initialButtons.Contains(d.Key+"/"+i)) continue;
                    var b=settings.Buttons[learningButton.Value]; b.Device=d.Key; b.Button=i;
                    learningButton=null; RefreshButtonRows(); Save();
                    status.Text=$"{d.InputName(i)} on {d.Label} assigned. Now pick the game key it should press.";
                }
            if(learningButton!=null && Environment.TickCount64>learnDeadline) {learningButton=null; status.Text="No button pressed. Try again.";}
        }
        if(calibration)
            foreach(var key in ranges.Keys.ToArray())
            {
                var b=settings.Axes[key]; var d=Find(b); if(d==null) continue;
                var r=ranges[key]; ranges[key]=(Math.Min(r.Min,d.Raw(b.Axis)),Math.Max(r.Max,d.Raw(b.Axis)));
            }
        bool focus=GameFocused();
        // Flight needs every assigned axis calibrated and connected; if one drops out the mod hands control back.
        bool flight=armed && flightCheck.Checked && AxesReady();
        // Buttons. Hold keeps the key down; Tap / Tap on/off send a short press on each change, so rotary selectors
        // and latched switches never hold a key forever. Edges are tracked even while disarmed, so arming with a
        // switch already on doesn't fire it.
        var wanted=new HashSet<string>(); bool reverseHeld=false;
        foreach(var b in settings.Buttons.Where(b=>b.Output!="None" && b.Button>=0))
        {
            var d=FindDevice(b.Device);
            bool on=d!=null && d.Pressed(b.Button), was=lastPressed.GetValueOrDefault(b);
            lastPressed[b]=on;
            if(b.Output==ButtonOutput.ReverseHold) { reverseHeld|=on; continue; }
            if(b.Output==ButtonOutput.ReverseToggle) { if(on && !was && armed) reverseToggled=!reverseToggled; continue; }
            // Taps are queued, each 3 ticks down then 2 up, so quick detents and fast on-off flips all register.
            var t=taps.GetValueOrDefault(b);
            if(on!=was && armed && focus && (b.Mode=="Tap on/off" || (b.Mode=="Tap" && on))) t.Pending=Math.Min(t.Pending+1,8);
            if(ticks>=t.Gap && t.Pending>0) { t.Pending--; t.Down=ticks+3; t.Gap=ticks+5; }
            taps[b]=t;
            if(armed && focus && ((b.Mode=="Hold" && on) || ticks<t.Down)) wanted.Add(b.Output);
        }
        buttonOutput.Update(wanted);
        var axes=new double[Settings.Movements.Length]; int mask=0, kbMask=0;
        for(int i=0;i<axes.Length;i++)
        {
            var b=settings.Axes[Settings.Movements[i]]; var d=Find(b);
            if(!b.Confirmed || d==null) continue;
            axes[i]=b.Normalize(d.Raw(b.Axis)); mask|=1<<i;
        }
        // While flying, the mod also drives roll, throttle, strafe and lift that aren't on a controller. That silences
        // the game's own joystick handling, which would otherwise fight the mod; the keyboard fills those in instead.
        foreach(var (i,(plus,minus)) in KeyboardKeys)
        {
            double target=flight && focus ? Key(plus)-Key(minus) : 0;
            keyboard[i]+=Math.Clamp(target-keyboard[i],-0.05,0.05);   // about 3 units/s, like the game's own smoothing
            if(!flight || (mask&(1<<i))!=0) continue;
            axes[i]=keyboard[i]; mask|=1<<i; kbMask|=1<<i;
        }
        if((kbMask&(1<<3))!=0) mask|=1<<6;   // keyboard throttle: the mod leaves the game's speed lock alone
        // Throttle, after the keyboard so this applies to a W/S throttle too: subtract the Reverse thrust lever,
        // then flip it while a reverse button is active.
        var rev=settings.Axes[Settings.Reverse];
        if(rev.Confirmed && Find(rev)!=null) { axes[3]-=AxisValue(Settings.Reverse); mask|=1<<3; }
        bool reverse=reverseHeld || reverseToggled;
        if(reverse) axes[3]=-axes[3];
        axes[3]=Math.Clamp(axes[3],-1,1);
        double[] head; bool fresh;
        lock(poseLock) {head=pose.Zip(zero,(v,z)=>v-z).ToArray(); fresh=poseTime>0 && Environment.TickCount64-poseTime<250;}
        bool tracking=headCheck.Checked && fresh, look=LookAssigned();
        if(!tracking) head=new double[6];
        // Look axes turn the cockpit view like head tracking (full deflection = 90° yaw, 60° pitch), with or
        // without OpenTrack. Pushing up/forward reads negative, so it is flipped to look up.
        double lookYaw=AxisValue("Look Yaw"), lookPitch=AxisValue("Look Pitch");
        head[3]+=lookYaw*90; head[4]-=lookPitch*60;
        for(int i=3;i<6;i++) head[i]=Math.Clamp(head[i],-180,180);   // the mod rejects frames with wilder angles
        WriteFrame(flight && focus,armed && (tracking || look) && focus,axes,head,mask);
        if(++ticks%10==0)
        {
            lines.Add("\r\nNormalized:  "+string.Join("   ",Settings.Movements.Select((m,i)=>(mask&(1<<i))==0 ? $"{m} —"
                : $"{m} {axes[i]:0.00}{((kbMask&(1<<i))!=0 ? " (keys)" : "")}"))+(reverse ? "   [REVERSE]" : ""));
            if(look) lines.Add($"Look:  yaw {lookYaw:0.00}   pitch {lookPitch:0.00}");
            lines.Add("\r\nIn-game mod: "+ReadModStatus());
            lines.Insert(0,"NEXT STEP: "+NextStep()+"\r\n");
            if(frameTrouble!=null) lines.Insert(0,"⚠ The game gets no controls: "+frameTrouble+"\r\n");
            monitor.Text=string.Join("\r\n",lines);
            headLabel.Text=(fresh ? "OpenTrack OK" : "OpenTrack: no signal")+(tracking||look ? $" · view yaw {head[3]:0}° pitch {head[4]:0}°" : "");
            if(armed) status.Text=frameTrouble!=null ? "⚠ "+frameTrouble : (!focus ? "Armed; waiting for the SpaceBourne 2 window."
                : flight ? "Armed — Ctrl+Alt+F12 disarms. Flight pauses by itself in menus, warp, landing and docking."
                : "Armed for head tracking and buttons only.")
                +(flightCheck.Checked && !flight ? " Flight is OFF until your axes are calibrated: DISARM, then Calibrate axes." : "");
        }
    }
    void WriteFrame(bool flight,bool head,double[] axes,double[] pose,int mask)
    {
        var path=Data.PathOf("state.txt");
        var frame=new FlightFrame(++sequence,flight,head,axes,pose,(double)turnRate.Value,mask);
        // A failed write skips this frame instead of disarming; the mod's watchdog allows 250 ms. The usual cause is
        // the mod reading state.txt right now, which Windows reports as "access denied" (not a sharing error).
        try
        {
            bool check=sequence%30==0;   // about twice a second, make sure the frame really landed
            Data.Replace(path,frame.Serialize(),check);
            if(check) frameTrouble=null;
        }
        catch(DirectoryNotFoundException) { Directory.CreateDirectory(Data.Dir); }
        catch(StuckFileException e) { frameTrouble=e.Message; }
        catch(Exception e) when (e is IOException or UnauthorizedAccessException) {}
    }
    string ReadModStatus()
    {
        try
        {
            using var s=new FileStream(Data.PathOf("status.txt"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            return new StreamReader(s).ReadToEnd().Trim();
        }
        catch(IOException) { return "no news yet — start SpaceBourne 2 with the mod installed"; }
    }
    void CloseBridge()
    {
        closing=true; timer.Stop(); Disarm(); cancel.Cancel(); udp?.Dispose();
        Native.UnregisterHotKey(Handle,1); Native.UnregisterHotKey(Handle,2);
        foreach(var d in devices) d.Dispose();
        if(sdl) Sdl.SDL_Quit();
    }
}

internal static class Native
{
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint id);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(IntPtr hWnd,int id);
}
