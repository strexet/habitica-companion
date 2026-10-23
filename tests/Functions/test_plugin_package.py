import unittest,tempfile,shutil,json,importlib.util
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('packager',ROOT/'deploy/package-plugin.py');package=importlib.util.module_from_spec(spec);spec.loader.exec_module(package)
class PackageTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup);self.root=Path(self.temp.name);self.source=self.root/'source';shutil.copytree(ROOT/'plugin',self.source);self.requirements=json.loads((ROOT/'deploy/plugin-requirements.json').read_text())
 def build(self,name='release'):
  target=self.root/name;package.build(self.source,target,'a'*40,self.requirements);return target
 def test_reproducible_archive_and_external_receipt(self):
  first=self.build('a');second=self.build('b');self.assertEqual(first.with_suffix('.zip').read_bytes(),second.with_suffix('.zip').read_bytes());self.assertEqual(package.verify(first),package.verify(second));self.assertEqual(package.verify_archive(first.with_suffix('.zip'))['contentHash'],package.verify(first)['contentHash'])
 def test_changed_payload_same_version_has_distinct_identity(self):
  first=self.build('a');p=self.source/'skills/habitica-task-management/SKILL.md';p.write_text(p.read_text()+'\nChanged fixture.\n');second=self.build('b');a=package.verify(first);b=package.verify(second);self.assertEqual(a['packageVersion'],b['packageVersion']);self.assertNotEqual(a['contentHash'],b['contentHash']);self.assertNotEqual(a['skills'],b['skills'])
 def test_stale_or_tampered_files_and_descriptors_refuse(self):
  target=self.build();p=target/'skills/habitica-account-review/SKILL.md';p.write_text(p.read_text()+'tamper');self.assertRaises(ValueError,package.verify,target)
 def test_skill_descriptor_mismatch_refuses(self):
  target=self.build();p=target/'skills/habitica-account-review/references/package-identity.json';p.write_text('{}');self.assertRaises(ValueError,package.verify,target)
 def test_archive_mismatch_refuses(self):
  target=self.build();archive=target.with_suffix('.zip');archive.write_bytes(archive.read_bytes()+b'tamper');self.assertRaises(ValueError,package.verify_archive,archive)
 def test_symlinks_and_credential_wiring_refuse(self):
  (self.source/'secret-link.json').symlink_to(self.source/'plugin.json');self.assertRaises(ValueError,self.build);(self.source/'secret-link.json').unlink();p=self.source/'mcp.json';r=json.loads(p.read_text());r['mcpServers']['habitica-companion']['headers']={'authorization':'synthetic-secret'};p.write_text(json.dumps(r));self.assertRaises(ValueError,self.build)
 def test_requirements_and_source_revision_are_explicit(self):
  self.assertRaises(ValueError,package.build,self.source,self.root/'release','unknown',self.requirements);self.requirements['secret']='not allowed';self.assertRaises(ValueError,self.build)
if __name__=='__main__':unittest.main()
