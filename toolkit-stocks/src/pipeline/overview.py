from lib.transforms import build_overview_row
from lib.module.printf import printf, level as lv
from lib.supabase.sql_functions import insert_data_statement
import constants.db_tables as dbt
from .context import refresh_ids


def run_overview(ctx):
	ds = ctx.ds
	yfs = ctx.yfs

	# DefeatBeta profiles (US) + officers for key executives
	profiles = ds.profiles(ctx.us_symbols)
	officers = ds.officers(ctx.us_symbols)

	# Identity fallback (name/exchange/currency) for symbols with no DB meta yet
	need_yf_identity = [s for s in ctx.us_symbols if not ctx.symbol_meta.get(s, {}).get("name")]
	yf_infos = yfs.map_symbols(yfs.info, need_yf_identity, "overview identity") if need_yf_identity else {}

	rows = []
	for symbol in ctx.us_symbols:
		profile = profiles.get(symbol)
		if not profile:
			printf(f"{symbol}: no DefeatBeta profile", lv.WARNING)
			continue
		rows.append(build_overview_row(
			symbol, profile, officers.get(symbol, []),
			ctx.symbol_meta.get(symbol, {}) or {},
			yf_infos.get(symbol),
		))

	# Non-US via yfinance info()
	def yf_profile(sym):
		return yfs.profile(sym)

	intl_infos = yfs.map_symbols(yf_profile, ctx.intl_symbols, "overview") if ctx.intl_symbols else {}
	for symbol, info in intl_infos.items():
		rows.append(build_overview_row(symbol, info, None, ctx.symbol_meta.get(symbol, {}) or {}, info))

	for row in rows:
		if not row:
			continue
		symbol = row["symbol"]
		mode_condition = {} if ctx.stock_ids.get(symbol) is None else {"symbol": symbol}
		insert_data_statement(row, dbt.stock, update_condition=mode_condition)

	refresh_ids(ctx)
