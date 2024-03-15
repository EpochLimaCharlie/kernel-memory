all: git_upstream git_upstream_merge

git_upstream:
	git fetch upstream

git_upstream_merge:
	git merge upstream/main