import datetime as dt

from lib.transforms import build_price_rows, build_dividend_rows, build_split_rows
from lib.functions.data_types import map_latest_yahoo_historical
from lib.module.printf import printf, level as lv
from lib.supabase.sql_functions import get_latest_dates, insert_data_statement
import constants.db_tables as dbt
from .context import valid_us, valid_intl

EPOCH = dt.date(2000, 1, 1)


def run_market_data(ctx):
	'''Prices + splits + dividends for US (DefeatBeta bulk) and non-US (yfinance).'''
	run_us(ctx)
	run_intl(ctx)


def run_us(ctx):
	ds = ctx.ds
	targets = valid_us(ctx)
	if not targets:
		return

	prices = ds.prices(targets)
	dividends = ds.dividends(targets)
	splits = ds.splits(targets)

	latest_price_dates = get_latest_dates(dbt.historical_price, [ctx.stock_ids[s] for s in targets if s in prices])
	latest_div_dates = get_latest_dates(dbt.dividend, [ctx.stock_ids[s] for s in targets if s in dividends])

	for symbol in targets:
		if symbol not in prices:
			continue
		printf(f"---- {symbol} HISTORICAL PRICES --", lv.INFO)
		insert_data_statement(
			build_price_rows(
				symbol, ctx.stock_ids[symbol], prices[symbol],
				dividends.get(symbol, []), splits.get(symbol, []),
				latest_price_dates.get(ctx.stock_ids[symbol]),
			),
			dbt.historical_price,
		)

	printf("---- SPLITS (US) --", lv.INFO)
	for symbol in targets:
		insert_data_statement(
			build_split_rows(symbol, ctx.stock_ids[symbol], splits.get(symbol, [])),
			dbt.stock_split,
		)

	printf("---- DIVIDENDS (US) --", lv.INFO)
	for symbol in targets:
		if symbol not in dividends:
			continue
		insert_data_statement(
			build_dividend_rows(
				symbol, ctx.stock_ids[symbol], dividends[symbol],
				latest_div_dates.get(ctx.stock_ids[symbol]),
			),
			dbt.dividend,
			null_tolerance=0.0
		)


def run_intl(ctx):
	yfs = ctx.yfs
	targets = valid_intl(ctx)
	if not targets:
		return

	def yf_market(sym):
		stock_id = ctx.stock_ids[sym]
		latest = get_latest_dates(dbt.historical_price, [stock_id]).get(stock_id) or EPOCH
		period = map_latest_yahoo_historical(latest) or "max"
		payload = yfs.history_payload(sym, period)
		if payload is None:
			return None
		payload["stock_id"] = stock_id
		payload["latest"] = latest
		return payload

	results = yfs.map_symbols(yf_market, targets, "prices/dividends/splits")

	for symbol, payload in results.items():
		printf(f"---- {symbol} HISTORICAL PRICES (yf) --", lv.INFO)
		insert_data_statement(
			build_price_rows(symbol, payload["stock_id"], payload["price_rows"], [], [], payload["latest"]),
			dbt.historical_price,
		)
		insert_data_statement(
			build_dividend_rows(symbol, payload["stock_id"], payload["div_events"], payload["latest"]),
			dbt.dividend,
			null_tolerance=0.0
		)
		insert_data_statement(
			build_split_rows(symbol, payload["stock_id"], payload["split_events"]),
			dbt.stock_split,
		)
