[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
$condition=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty,'NuGet Gallery | Trusted Publishing - Google Chrome')
$w=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
if(!$w){throw 'Expected NuGet window missing'}

function FindId($id){
 $c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$id)
 $e=$w.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$c)
 if(!$e){throw "Missing expected field: $id"};return $e
}
$fields=[ordered]@{
 '__policy-name-0'='Hexalith.Builds 4.44 maintenance'
 '__github-repository-owner-0'='Hexalith'
 '__github-repository-0'='Hexalith.Builds'
 '__github-workflow-file-0'='build-release.yml'
 '__github-environment-0'='production'
 '__scope-subjects-input-0'='Hexalith.Builds*'
}

$fields['__package-owner-0']='Hexalith'
$fields['__provider-0']='GitHub Actions'
foreach($id in $fields.Keys){
 $e=FindId $id
 $value=$e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
 if($value -cne $fields[$id]){throw "Field differs from approved value: $id"}
}
$push=FindId '__push-scope-checked-0'
if($push.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On){throw 'Push scope not selected'}
$radio=FindId '__package-push-scope-0'
if(!$radio.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected){throw 'Original push type missing'}
foreach($id in @('__unlist-scope-checked-0','__stage-scope-checked-0')){
 $e=FindId $id
 if($e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off){throw 'Unexpected scope enabled'}
}
$all=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
$names=@($all|ForEach-Object {$_.Current.Name})
$start=[Array]::IndexOf($names,'Hexalith.Builds builds-release')
if($start -lt 0){throw 'Existing policy missing'}
$original=@($names[$start..([Math]::Min($start+23,$names.Count-1))])
foreach($expected in @('Hexalith','Hexalith.Builds*','Push new packages and package versions','Workflow: builds-release.yml','Environment: hexalith-release')){
 if($original -cnotcontains $expected){throw "Existing policy changed: $expected"}
}
$buttons=@($all|Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $_.Current.Name -ceq 'Create' -and $_.Current.AutomationId -eq ''})
if($buttons.Count -ne 1){throw 'Unique submit Create control missing'}
$button=$buttons[0]
$button.GetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern).ScrollIntoView()
$invoke=$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
'Approved fields and preserved policy verified; invoking one Create submission'
$invoke.Invoke()
'Create invoked once'
