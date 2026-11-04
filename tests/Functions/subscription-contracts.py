"""Local composition only: actual Workers/D1 and native local TLS, synthetic host."""
import json, os, pathlib, re, signal, subprocess, sys, time

root = pathlib.Path(__file__).resolve().parents[2]
report = pathlib.Path(sys.argv[1]).resolve()
report.parent.mkdir(parents=True, exist_ok=True)
commands = [
    ("build-workspace", "workers/companion-workspace", ["npm", "run", "build"], 120),
    ("build-auth", "workers/chatgpt-auth", ["npm", "run", "build"], 120),
    ("build-mcp", "workers/chatgpt-mcp", ["npm", "run", "build"], 120),
    ("subscription-worker-composition", "workers/chatgpt-auth", ["node", "--experimental-strip-types", "--test", "--test-timeout=30000", "test/events.test.mjs", "test/callback-egress.test.mjs", "test/mcp2.test.mjs"], 180),
    ("gateway-native-tls", "deploy/callback-egress", ["npm", "test"], 90),
    ("independent-receiver", ".", ["node", "--experimental-strip-types", "--test", "--test-timeout=30000", "tests/Functions/callback-receiver.test.mjs"], 60),
]
results = []
for name, cwd, argv, deadline in commands:
    started = time.monotonic()
    try:
        p = subprocess.Popen(argv, cwd=root/cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, start_new_session=True)
        stdout, stderr = p.communicate(timeout=deadline)
        output, code = stdout+stderr, p.returncode
    except subprocess.TimeoutExpired:
        os.killpg(p.pid, signal.SIGKILL)
        stdout, stderr = p.communicate(timeout=15)
        output, code = stdout+stderr+"Hard deadline exceeded; owned process group terminated.\n", 124
    log = report.with_name(report.stem+"-"+name+".log")
    log.write_text(output)
    tests = sum(map(int, re.findall(r"^# tests (\d+)$", output, re.M)))
    passed = sum(map(int, re.findall(r"^# pass (\d+)$", output, re.M)))
    skipped = sum(map(int, re.findall(r"^# skipped (\d+)$", output, re.M)))
    results.append(dict(name=name, exit_code=code, seconds=round(time.monotonic()-started, 3), tests=tests, passed=passed, skipped=skipped, log=str(log.relative_to(root))))
    report.write_text(json.dumps(dict(scope="local-synthetic-composition-no-real-host", results=results, result="pass" if len(results)==len(commands) and all(r["exit_code"]==0 and (r["name"].startswith("build-") or r["tests"]>0 and r["passed"]==r["tests"] and r["skipped"]==0) for r in results) else "fail" if code else "running"), indent=2)+"\n")
    print(json.dumps(results[-1]), flush=True)
    if code or name not in ["build-workspace", "build-auth", "build-mcp"] and (not tests or passed!=tests or skipped):
        sys.exit(1)
