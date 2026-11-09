from pathlib import Path
p=Path('/tmp/story-3-2-broad-20261109-_z_wy84q/current');dest=Path('/tmp/story32-default-source-mode-patch');dest.mkdir(exist_ok=False)
s=(p/'Directory.Build.targets').read_text();s=s.replace(" and '$(HexalithEventStoreVersion)' != ''",'');lines=[]
for line in s.splitlines():
 if '<AdditionalProperties Condition=' in line and 'HexalithEventStoreVersion' in line:
  line=line.replace('Condition="','Condition="\'$(HexalithEventStoreVersion)\' != \'\' and ',1);lines.append(line);indent=line[:len(line)-len(line.lstrip())];lines.append(indent+'''<AdditionalProperties Condition="!$([System.Text.RegularExpressions.Regex]::IsMatch('%(ProjectReference.AdditionalProperties)', '(?i)(^|;)\\s*UseHexalithProjectReferences\\s*='))">%(ProjectReference.AdditionalProperties);UseHexalithProjectReferences=$(UseHexalithProjectReferences)</AdditionalProperties>''')
 else:lines.append(line)
s='\n'.join(lines)+'\n';(dest/'Directory.Build.targets').write_text(s)
s=(p/'eng/test_source_sdk_propagation.py').read_text();s=s.replace('def invoke(cls, project, properties, target=None):','def invoke(cls, project, properties, target=None, configuration="Release"):');s=s.replace('"-p:Configuration=Release",','"-p:Configuration=" + configuration,');s=s.replace('        self.assertEqual(self.explicit, entries["Hexalith.EventStore.Contracts"].get("AdditionalProperties"))\n','');s=s.replace('self.default_sdk, metadata)','self.default_sdk + ";UseHexalithProjectReferences=true", metadata)');s=s.replace('";HexalithEventStoreVersion=4.0.4", metadata','";HexalithEventStoreVersion=4.0.4;UseHexalithProjectReferences=true", metadata')
s=s.replace('''        self.selection(["-p:UseHexalithProjectReferences=true"])

    def test_package_mode''','''        document, _ = self.selection(["-p:UseHexalithProjectReferences=true"])
        contracts = next(item for item in document["Items"]["ProjectReference"] if item["Filename"] == "Hexalith.EventStore.Contracts")
        self.assertEqual(self.explicit + ";UseHexalithProjectReferences=true", contracts["AdditionalProperties"])

    def test_package_mode''')
s=s.replace('''            self.assertEqual([self.default_sdk], values)
''','''            self.assertEqual([self.default_sdk], values)
            modes = re.findall(r"(?:^|;)\\s*UseHexalithProjectReferences\\s*=([^;]+)", item.get("AdditionalProperties", ""), re.IGNORECASE)
            self.assertEqual(["true"], modes)
''')
insert='''    def child_selection(self, assignments="", global_properties=None):
        directory = Path(self.temporary.name)
        leaf = directory / "observe-child.targets"
        leaf.write_text("<Project><Target Name='ObserveSourceModeLeaf' Returns='@(_ObservedSourceMode)'><ItemGroup><_ObservedSourceMode Include='$(MSBuildProjectName)'><Mode>$(UseHexalithProjectReferences)</Mode><Sdk>$(HexalithEventStoreVersion)</Sdk><EventStoreFromSource>$(HexalithEventStoreFromSource)</EventStoreFromSource></_ObservedSourceMode></ItemGroup></Target></Project>")
        project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
        group = ET.SubElement(project, "ItemGroup")
        reference = ET.SubElement(group, "ProjectReference", Include=str(ROOT / "references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Hexalith.Tenants.Contracts.csproj"))
        if assignments:
            ET.SubElement(reference, "AdditionalProperties").text = assignments
        target = ET.SubElement(project, "Target", Name="ObserveChildSourceMode", Returns="@(_ObservedChildSourceMode)")
        task = ET.SubElement(target, "MSBuild", Projects="@(ProjectReference)", Targets="ObserveSourceModeLeaf", BuildInParallel="false",
                             Properties="%(ProjectReference.AdditionalProperties)",
                             RemoveProperties="DirectoryBuildPropsPath;DirectoryBuildTargetsPath;DirectoryPackagesPropsPath")
        ET.SubElement(task, "Output", TaskParameter="TargetOutputs", ItemName="_ObservedChildSourceMode")
        fixture = directory / ("ChildSelection-" + str(len(self.evaluations)) + ".csproj")
        fixture.write_bytes(ET.tostring(project, encoding="utf-8"))
        document = self.invoke(fixture, ["-p:CustomAfterMicrosoftCommonTargets=" + str(leaf),
                              "-getTargetResult:ObserveChildSourceMode", *(global_properties or [])],
                              "ObserveChildSourceMode", configuration="Debug")
        items = document["TargetResults"]["ObserveChildSourceMode"]["Items"]
        self.assertEqual(1, len(items))
        self.assertEqual("Hexalith.Tenants.Contracts", items[0]["Identity"])
        return items[0]

    def test_default_debug_source_mode_reaches_actual_owner_without_global_override(self):
        child = self.child_selection()
        self.assertEqual("true", child["Mode"])
        self.assertEqual(self.default_sdk, child["Sdk"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_explicit_global_source_mode_reaches_actual_owner(self):
        child = self.child_selection(global_properties=["-p:UseHexalithProjectReferences=true"])
        self.assertEqual("true", child["Mode"])
        self.assertEqual(self.default_sdk, child["Sdk"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_explicit_global_package_mode_keeps_actual_owner_in_package_mode(self):
        child = self.child_selection(global_properties=["-p:UseHexalithProjectReferences=false"])
        self.assertEqual("false", child["Mode"])
        self.assertNotEqual("true", child.get("EventStoreFromSource", ""))

    def test_case_insensitive_explicit_package_edge_keeps_its_own_sdk(self):
        child = self.child_selection("ControlContext=keep;useHEXALITHProjectReferences=false;hexalithEventStoreVersion=4.1.0")
        self.assertEqual("false", child["Mode"])
        self.assertEqual("4.1.0", child["Sdk"])
        self.assertNotEqual("true", child.get("EventStoreFromSource", ""))

    def test_explicit_sdk_only_edge_still_inherits_default_debug_source_mode(self):
        child = self.child_selection("ControlContext=keep;hexalithEventStoreVersion=4.1.0")
        self.assertEqual("true", child["Mode"])
        self.assertEqual("4.1.0", child["Sdk"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_source_mode_propagation_is_independent_of_an_empty_sdk_selection(self):
        child = self.child_selection(global_properties=["-p:HexalithEventStoreVersion="])
        self.assertEqual("true", child["Mode"])
        self.assertEqual("true", child["EventStoreFromSource"])

    def test_default_debug_sdk_promoted_edges_keep_source_mode_without_global_override(self):
        project = ROOT / "tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj"
        before = self.invoke(project, [], configuration="Debug")["Items"]["ProjectReference"]
        after = self.invoke(project, [], "IncludeTransitiveProjectReferences", configuration="Debug")["Items"]["ProjectReference"]
        previous = {item["FullPath"] for item in before}
        promoted = [item for item in after if item["FullPath"] not in previous]
        self.assertTrue(promoted)
        for item in promoted:
            modes = re.findall(r"(?:^|;)\\s*UseHexalithProjectReferences\\s*=([^;]+)", item.get("AdditionalProperties", ""), re.IGNORECASE)
            self.assertEqual(["true"], modes)


'''
s=s.replace('\ndef main():','\n'+insert+'def main():');s=s.replace('!= 9:', '!= 16:').replace('exactly nine controls.', 'exactly sixteen controls.').replace('result.testsRun == 9','result.testsRun == 16').replace('{"expected": 9,','{"expected": 16,');(dest/'test_source_sdk_propagation.py').write_text(s);compile(s,'source-mode-controls','exec');print(dest)
