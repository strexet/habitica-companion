import unittest,tempfile,subprocess,shutil,json
from pathlib import Path
class BuildIdentityTests(unittest.TestCase):
 def test_stale_metadata_and_reviewed_inputs_fail_closed(self):
  with tempfile.TemporaryDirectory() as directory:
   root=Path(directory);(root/'scripts').mkdir();(root/'shared').mkdir()
   generator=root/'scripts/generate-build-identity.py';shutil.copy(Path(__file__).with_name('generate-build-identity.py'),generator)
   release={'backendRelease':'0.1.0','toolSchemaVersion':'1.0.0','supportedPackage':{'minimum':'0.1.0','maximumExclusive':'0.2.0'}}
   (root/'release-inputs.json').write_text(json.dumps(release));source=root/'shared/fixture.ts';source.write_text('export const value=1;')
   def run(*args):return subprocess.run(['python3',str(generator),*args],capture_output=True,text=True,timeout=5)
   self.assertEqual(run().returncode,0);self.assertEqual(run('--check').returncode,0)
   original=(root/'shared/build-identity.generated.ts').read_text();self.assertEqual(run().returncode,0);self.assertEqual(original,(root/'shared/build-identity.generated.ts').read_text())
   source.write_text('export const value=2;');self.assertNotEqual(run('--check').returncode,0);self.assertEqual(run().returncode,0);self.assertNotEqual(original,(root/'shared/build-identity.generated.ts').read_text())
   release['unexpectedSecret']='must not propagate';(root/'release-inputs.json').write_text(json.dumps(release));self.assertNotEqual(run().returncode,0)
if __name__=='__main__':unittest.main()
