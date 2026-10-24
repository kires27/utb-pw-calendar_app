from lib.transforms import build_financial_analysis, build_quality_score
from lib.module.printf import printf, level as lv
from lib.supabase.sql_functions import insert_data_statement
import constants.db_tables as dbt
from .context import valid_us, valid_intl
from .quotes import build_us_quote


def run_financial_analysis(ctx):
	yfs = ctx.yfs

	for symbol in valid_us(ctx):
		printf(f"---- {symbol} FINANCIAL ANALYSIS --", lv.INFO)
		insert_and_score(ctx, symbol, build_us_quote(ctx, symbol) or {})

	def yf_quote(sym):
		return yfs.quote(sym)

	quotes = yfs.map_symbols(yf_quote, valid_intl(ctx), "fa quotes") if ctx.intl_symbols else {}
	for symbol, quote in quotes.items():
		printf(f"---- {symbol} FINANCIAL ANALYSIS --", lv.INFO)
		insert_and_score(ctx, symbol, quote or {})


def insert_and_score(ctx, symbol, quote):
	stock_id = ctx.stock_ids[symbol]

	fa = build_financial_analysis(symbol, stock_id, quote)
	if not fa:
		return

	update_condition = {
		"stock_id": stock_id,
		"fiscal_date": fa["fiscal_date"],
		"period": fa["period"]
	}

	insert_data_statement(fa["data"], dbt.analysis, update_condition=update_condition)

	fqs = build_quality_score(stock_id)
	insert_data_statement(fqs["data"], dbt.analysis, update_condition=update_condition)
