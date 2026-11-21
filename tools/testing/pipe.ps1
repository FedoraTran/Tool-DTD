param([string[]]$Commands)
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "DTDTrainer.Pipe", [System.IO.Pipes.PipeDirection]::InOut)
$pipe.Connect(3000)
$enc = New-Object System.Text.UTF8Encoding($false)
$w = New-Object System.IO.StreamWriter($pipe, $enc); $w.AutoFlush = $true; $w.NewLine = "`n"
$r = New-Object System.IO.StreamReader($pipe, $enc)
foreach ($c in $Commands) { $w.WriteLine($c); "> $c"; $r.ReadLine() }
$pipe.Dispose()
