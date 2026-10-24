import math

from lib.transforms.prices import _clean, compute_adj_close, build_price_rows


def test_clean_none_and_nan():
	assert _clean(None) is None
	assert _clean(float("nan")) is None
	assert _clean("abc") is None
	assert _clean("12.5") == 12.5
	assert _clean(0) == 0.0


def test_adj_close_no_events_equals_close():
	rows = [
		{"date": "2024-01-03", "close": 105.0},
		{"date": "2024-01-02", "close": 100.0},
	]
	assert compute_adj_close(rows, [], []) == {
		"2024-01-03": 105.0,
		"2024-01-02": 100.0,
	}


def test_adj_close_dividend_scales_older_prices():
	# Dividend on 2024-01-03: rows before it scale by (1 - div/close_after)
	rows = [
		{"date": "2024-01-03", "close": 100.0},
		{"date": "2024-01-02", "close": 100.0},
		{"date": "2024-01-01", "close": 50.0},
	]
	divs = [{"date": "2024-01-03", "amount": 10.0}]
	out = compute_adj_close(rows, divs, [])
	assert out["2024-01-03"] == 100.0
	# close_after = 100 (nearest row after event, walking backwards)
	assert math.isclose(out["2024-01-02"], 100.0 * (1 - 10 / 100))
	assert math.isclose(out["2024-01-01"], 50.0 * (1 - 10 / 100))


def test_adj_close_split_factor():
	rows = [
		{"date": "2024-01-03", "close": 50.0},
		{"date": "2024-01-02", "close": 100.0},
	]
	# 2:1 split on newest date -> older close halved in adj terms
	out = compute_adj_close(rows, [], [{"date": "2024-01-03", "factor": 2}])
	assert out["2024-01-03"] == 50.0
	assert math.isclose(out["2024-01-02"], 100.0 / 2)


def test_adj_close_event_on_holiday_no_price_row():
	# Event date has no price row - applies to all strictly older rows
	rows = [
		{"date": "2024-01-05", "close": 100.0},
		{"date": "2024-01-02", "close": 100.0},
	]
	out = compute_adj_close(rows, [{"date": "2024-01-04", "amount": 5.0}], [])
	# last_seen_close for the dividend is the 01-05 close (100)
	assert math.isclose(out["2024-01-02"], 100.0 * (1 - 5 / 100))


def test_adj_close_skips_bad_close_rows():
	rows = [
		{"date": "2024-01-03", "close": None},
		{"date": "2024-01-02", "close": 100.0},
	]
	assert compute_adj_close(rows, [], [])["2024-01-02"] == 100.0


def test_build_price_rows_incremental_filter():
	rows = [
		{"date": "2024-01-03", "open": 1, "high": 2, "low": 0.5, "close": 100, "volume": 10},
		{"date": "2024-01-02", "close": 99},
	]
	out = build_price_rows("TEST", 7, rows, [], [], "2024-01-03")
	assert len(out) == 1
	assert out[0]["stock_id"] == 7
	assert out[0]["date"] == "2024-01-03"
	assert out[0]["adj_close"] == 100.0


def test_build_price_rows_stock_id_none_returns_empty():
	rows = [{"date": "2024-01-03", "close": 100}]
	assert build_price_rows("TEST", None, rows, [], [], None) == []


def test_build_price_rows_none_latest_returns_all():
	rows = [{"date": "2024-01-03", "close": 100}, {"date": "2024-01-02", "close": 99}]
	out = build_price_rows("TEST", 1, rows, [], [], None)
	assert [r["date"] for r in out] == ["2024-01-03", "2024-01-02"]
