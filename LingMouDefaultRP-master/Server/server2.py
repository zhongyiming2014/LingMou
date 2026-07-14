from flask import Flask, request, jsonify
from openai import OpenAI
import json
import os
import time

app = Flask(__name__)
TIMING_LOG_PATH = os.path.join(os.path.dirname(__file__), "timing.log")

DEEPSEEK_API_KEY = os.environ.get("DEEPSEEK_API_KEY")
DEEPSEEK_MODEL = os.environ.get("DEEPSEEK_MODEL", "deepseek-reasoner")

client = OpenAI(
    api_key=DEEPSEEK_API_KEY,
    base_url="https://api.deepseek.com",
) if DEEPSEEK_API_KEY else None


def log_timing(message):
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
    with open(TIMING_LOG_PATH, "a", encoding="utf-8") as log_file:
        log_file.write(f"{timestamp} {message}\n")


@app.route("/ask_action", methods=["POST"])
def ask_action():
    request_started = time.perf_counter()

    if not DEEPSEEK_API_KEY:
        return jsonify({"error": "DEEPSEEK_API_KEY is not set"}), 500

    data = request.get_json(silent=True)
    if not data:
        return jsonify({"error": "Invalid or empty JSON body"}), 400

    print(f"Received request: {data}", flush=True)

    try:
        robot_pos = data["robot_pos"]
        robot_face = data["robot_face"]
        crate_pos = data["crate_pos"]
    except KeyError as exc:
        return jsonify({"error": f"Missing field: {exc.args[0]}"}), 400

    system_prompt = """
You are a path-planning system for a 2D grid warehouse robot.
Return a pure JSON string array of actions that moves the robot from its current position to a cell adjacent to the crate, turns it to face the crate, and then picks it up.

Coordinate system:
- North: +Y
- South: -Y
- East: +X
- West: -X

Available actions:
- MOVE_FORWARD
- TURN_LEFT
- TURN_RIGHT
- PICK_UP

Rules:
- PICK_UP must be the final action.
- Before PICK_UP, the robot must stand in one of the four neighboring cells of the crate and face the crate.
- Prefer a short, deterministic route.
- Output only a JSON array, with no Markdown and no explanation.
"""

    user_prompt = f"""
Plan a route.
Robot_Pos: {robot_pos}
Robot_Face: {robot_face}
Target_Crate_Pos: {crate_pos}
Return the JSON action list only.
"""

    try:
        model_started = time.perf_counter()
        response = client.chat.completions.create(
            model=DEEPSEEK_MODEL,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            stream=False,
            temperature=0.0,
        )
        model_seconds = time.perf_counter() - model_started

        content = response.choices[0].message.content
        print(f"Raw model response: {content}", flush=True)

        clean_content = content.replace("```json", "").replace("```", "").strip()
        actions = json.loads(clean_content)

        total_seconds = time.perf_counter() - request_started
        print(
            f"[timing] model_seconds={model_seconds:.2f}, total_seconds={total_seconds:.2f}, model={DEEPSEEK_MODEL}",
            flush=True,
        )
        log_timing(
            f"model_seconds={model_seconds:.2f}, total_seconds={total_seconds:.2f}, model={DEEPSEEK_MODEL}, actions={actions}"
        )

        return jsonify({"actions": actions})
    except Exception as exc:
        total_seconds = time.perf_counter() - request_started
        print(f"Error after {total_seconds:.2f}s: {exc}", flush=True)
        log_timing(f"error_after={total_seconds:.2f}, error={exc}")
        return jsonify({"error": str(exc)}), 500


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=8765)