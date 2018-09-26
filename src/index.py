from flask import Flask
from api.query import simple_page
from services.durable import setUp


def create_app():
    app = Flask(__name__)
    app.register_blueprint(simple_page)

    return app


if __name__ == "__main__":
    app = create_app()
    setUp()
    app.run()

