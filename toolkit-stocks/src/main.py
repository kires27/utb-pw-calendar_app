import argparse, sys, os, warnings
from lib.module.printf import printf, level as lv
from dotenv import load_dotenv

# yfinance internals use pandas-deprecated Timestamp.utcnow; noise only
warnings.filterwarnings("ignore", message="Timestamp.utcnow is deprecated.*")


if os.getenv("GITHUB_ACTIONS") != "true":
	printf.open("log", create_log=True, console_print=True)
	load_dotenv()
else:
	printf.open("log", create_log=False, console_print=True)


parser = argparse.ArgumentParser(description="MICE script to insert/update data to database")
parser.add_argument("--overview", action="store_true", help="Insert overview data")
parser.add_argument("--balance", action="store_true", help="Insert balance sheet data")
parser.add_argument("--cash", action="store_true", help="Insert cash flow data")
parser.add_argument("--income", action="store_true", help="Insert income statement data")
parser.add_argument("--dividends", action="store_true", help="Insert dividends data")
parser.add_argument("--prices", action="store_true", help="Insert prices data")
parser.add_argument("--splits", action="store_true", help="Insert stock splits data")
parser.add_argument("--metrics", action="store_true", help="Insert metrics data")
parser.add_argument("--fa", action="store_true", help="Insert financial analysis data")
args = parser.parse_args()

if not (
	args.overview or
	args.balance or
	args.cash or
	args.income or
	args.dividends or
	args.prices or
	args.splits or
	args.metrics or
	args.fa
):
	printf("Nothing to update. Missing arguments!", lv.ERROR)
	printf.close()
	sys.exit(1)


from pipeline import run

run(args)
