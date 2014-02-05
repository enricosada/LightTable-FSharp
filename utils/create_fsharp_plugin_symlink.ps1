
function Lt-PluginsDir 
{
	$lt_appdata_dir = [IO.Path]::Combine([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData), 'LightTable')

	return [IO.Path]::Combine($lt_appdata_dir, 'plugins\F#')
}


function Lt-FSharp-Create-PluginDirSymlink 
{
	$cmd = "cmd /c mklink /d ""$(Lt-PluginsDir)"" ""$pwd"""
	
	echo $cmd
	iex $cmd
}

function Lt-FSharp-Remove-PluginDirSymlink 
{
    $cmd = "cmd /c rmdir ""$(Lt-PluginsDir)"" "
	
	echo $cmd
	iex $cmd
}
