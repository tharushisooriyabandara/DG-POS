package generate

import "strings"

type name struct {
	FirstName string
	LastName  string
}

func Name(fullName string) name {
	n := fullName
	firstName := n
	lastName := ""
	if strings.Contains(n, " ") {
		nameSplit := strings.Split(n, " ")
		firstName = nameSplit[0]
		lastName = strings.Join(nameSplit[1:], "")
	}

	return name{
		FirstName: firstName,
		LastName:  lastName,
	}
}
