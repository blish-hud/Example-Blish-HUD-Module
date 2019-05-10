param ([string]$project, [string]$output, [string]$mode)

Write-Output "Mode is $mode"

#if ($mode -eq "Debug") {
#    Write-Output "$($project)ref >>> $($output)ref"
#    Copy-Item -Path "$($project)ref" -Destination "ref" -Recurse -Force
#} else {
    Compress-Archive -Path "$($project)ref\*" -Update -DestinationPath "$($project)ref.zip"
    Move-Item "$($project)ref.zip" "$($project)ref.dat" -force
#}