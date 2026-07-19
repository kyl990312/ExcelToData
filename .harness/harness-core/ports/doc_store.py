"""DocStore 포트 (문서 read/write 추상). 본체는 이 인터페이스에만 의존한다.

DocRef = 백엔드 중립 논리 키 (예: "plan/<item>", "report/<item>").
1차 어댑터: adapters.local.LocalDocStore. 2차: notion 등.
"""
from abc import ABC, abstractmethod


class DocStore(ABC):
    @abstractmethod
    def read(self, ref):
        """ref 문서 내용 반환. 없으면 None."""

    @abstractmethod
    def write(self, ref, text):
        """ref 문서에 text 기록 (없으면 생성)."""

    @abstractmethod
    def exists(self, ref):
        """ref 문서 존재 여부."""

    @abstractmethod
    def list(self, prefix):
        """prefix 하위 ref 목록."""

    def search(self, query):
        raise NotImplementedError("search는 1차 미구현 (2차)")
